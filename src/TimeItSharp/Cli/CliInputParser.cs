using System.Text;

namespace TimeItSharp.Cli;

/// <summary>
/// The two ways in which the command line can be interpreted by TimeItSharp.
/// </summary>
public enum CliInputKind
{
    Configuration,
    Command,
}

/// <summary>
/// Result of classifying the user's primary input.
/// </summary>
public sealed record CliInput(CliInputKind Kind, string Value, bool IsExplicit)
{
    public bool IsConfiguration => Kind == CliInputKind.Configuration;

    public bool IsCommand => Kind == CliInputKind.Command;
}

/// <summary>
/// Process name and the argument text that should be passed to CliWrap.
/// </summary>
public readonly record struct ProcessCommand(string ProcessName, string ProcessArguments);

/// <summary>
/// Parses and classifies the primary TimeItSharp input without depending on System.CommandLine.
/// Keeping this logic here makes the mode decision independently testable and, importantly,
/// prevents a ".json" suffix in a process argument from selecting configuration mode.
/// </summary>
public static class CliInputParser
{
    private const int MaximumCommandLineCharacters = 1024 * 1024;
    private const int MaximumExecutablePathProbes = 256;
    internal const string MissingOptionValue = "\u001Ftimeitsharp-missing-option-value";
    /// <summary>
    /// Classifies a primary input as a configuration path or a process command.
    /// </summary>
    /// <param name="argumentValue">The legacy positional argument, if one was supplied.</param>
    /// <param name="configurationPath">A value supplied by <c>--config</c>.</param>
    /// <param name="commandValue">A value supplied by <c>--command</c>.</param>
    /// <param name="commandTerminated">Whether the value followed the conventional <c>--</c>
    /// option terminator.</param>
    public static CliInput Classify(
        string? argumentValue,
        string? configurationPath = null,
        string? commandValue = null,
        bool commandTerminated = false)
    {
        if (configurationPath is not null && commandValue is not null)
        {
            throw new ArgumentException("--config and --command cannot be used together.");
        }

        if (configurationPath is not null)
        {
            if (string.IsNullOrWhiteSpace(configurationPath))
            {
                throw new ArgumentException("A configuration path is required.", nameof(configurationPath));
            }

            return new CliInput(CliInputKind.Configuration, configurationPath, IsExplicit: true);
        }

        if (commandValue is not null)
        {
            if (string.IsNullOrWhiteSpace(commandValue))
            {
                throw new ArgumentException("A process name or command is required.", nameof(commandValue));
            }

            return new CliInput(CliInputKind.Command, commandValue, IsExplicit: true);
        }

        if (commandTerminated)
        {
            if (string.IsNullOrWhiteSpace(argumentValue))
            {
                throw new ArgumentException("A process name or command is required.", nameof(argumentValue));
            }

            return new CliInput(CliInputKind.Command, argumentValue, IsExplicit: true);
        }

        if (string.IsNullOrWhiteSpace(argumentValue))
        {
            throw new ArgumentException("A configuration file, process name, or command is required.", nameof(argumentValue));
        }

        // A .json suffix on the complete legacy value is the original configuration contract,
        // even when the file is missing or the value contains spaces. Consequently a legacy
        // command string such as "echo path/file.json" is configuration input; use --command or
        // -- to select command mode explicitly. Existing extensionless JSON files remain inferred.
        var completeValue = argumentValue.Trim();
        if (IsConfigurationPath(completeValue))
        {
            return new CliInput(CliInputKind.Configuration, completeValue, IsExplicit: false);
        }

        var processCommand = ParseProcessCommand(argumentValue);
        if (string.IsNullOrEmpty(processCommand.ProcessArguments))
        {
            var processPath = processCommand.ProcessName;
            if (IsConfigurationPath(processPath))
            {
                return new CliInput(CliInputKind.Configuration, processPath, IsExplicit: false);
            }
        }

        // More than one token is unambiguously a process command. In particular, the common
        // "echo hello.json" form must never be sent to Config.LoadConfiguration.
        return new CliInput(CliInputKind.Command, argumentValue, IsExplicit: false);
    }

    /// <summary>
    /// Converts arguments after a standalone <c>--</c> into one explicit command option. The
    /// System.CommandLine parser then cannot consume a process argument such as <c>--config</c>
    /// as one of TimeItSharp's options.
    /// </summary>
    public static string[] NormalizeArguments(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var separator = -1;
        for (var index = 0; index < args.Count; index++)
        {
            if (string.Equals(args[index], "--", StringComparison.Ordinal))
            {
                separator = index;
                break;
            }
        }

        var prefixCount = separator < 0 ? args.Count : separator;
        var normalized = new List<string>(args.Take(prefixCount));
        for (var index = 0; index < normalized.Count; index++)
        {
            if ((string.Equals(normalized[index], "--command", StringComparison.Ordinal) ||
                 string.Equals(normalized[index], "--config", StringComparison.Ordinal)) &&
                (index + 1 >= normalized.Count || normalized[index + 1].StartsWith("-", StringComparison.Ordinal)))
            {
                // Do not let a missing scalar value consume the next TimeItSharp option. The
                // explicit empty value is validated by the handler, while --help remains help.
                normalized[index] += "=" + MissingOptionValue;
            }
        }

        if (separator < 0)
        {
            return normalized.ToArray();
        }

        var commandTokens = args.Skip(separator + 1).ToArray();
        // Values following -- are discrete argv values, even when there is only one. Serialize
        // every boundary explicitly so later command parsing cannot reinterpret them based on
        // whitespace or files that happen to exist in the current directory.
        // If --command was already supplied, append the terminated argv values to that command
        // instead of emitting a second --command option. System.CommandLine quite correctly
        // rejects duplicate scalar options, but `--command app -- arg` is a useful and documented
        // spelling that must retain both pieces.
        var commandIndex = normalized.FindIndex(value => string.Equals(value, "--command", StringComparison.Ordinal) ||
                                                          value.StartsWith("--command=", StringComparison.Ordinal));
        var suffix = JoinArgumentValues(commandTokens);
        if (commandIndex >= 0)
        {
            var commandValue = string.Equals(normalized[commandIndex], "--command", StringComparison.Ordinal)
                ? string.Empty
                : normalized[commandIndex][("--command=".Length)..];
            if (string.Equals(commandValue, MissingOptionValue, StringComparison.Ordinal))
            {
                commandValue = string.Empty;
            }

            if (string.Equals(normalized[commandIndex], "--command", StringComparison.Ordinal))
            {
                if (commandIndex + 1 < normalized.Count &&
                    !normalized[commandIndex + 1].StartsWith("-", StringComparison.Ordinal))
                {
                    commandValue = normalized[commandIndex + 1];
                    normalized.RemoveAt(commandIndex + 1);
                }

                normalized[commandIndex] = "--command=" + commandValue;
            }

            if (!string.IsNullOrEmpty(suffix))
            {
                normalized[commandIndex] = "--command=" +
                    (string.IsNullOrEmpty(commandValue) ? suffix : $"{commandValue} {suffix}");
            }

            return normalized.ToArray();
        }

        // Use the equals form so a command whose executable starts with '-' is still consumed as
        // the option value rather than being parsed as another System.CommandLine option. The
        // value is already one argv item, so spaces do not need shell-level quoting here.
        normalized.Add($"--command={suffix}");
        return normalized.ToArray();
    }

    /// <summary>
    /// Joins positional argv values into the command-line text consumed by CliWrap. A single
    /// value is already a complete command string (the legacy quoted-command form); multiple
    /// values represent argv boundaries and values containing whitespace are quoted.
    /// </summary>
    public static string JoinCommandArguments(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (arguments.Count == 0)
        {
            return string.Empty;
        }

        if (arguments.Count == 1)
        {
            return arguments[0];
        }

        // Multiple positional values are discrete argv values. The single-value case above is
        // the documented legacy complete-command-string form; no File.Exists-dependent guessing
        // is allowed once the caller supplied more than one argv value.
        return string.Join(" ", arguments.Select(token => QuoteTokenForCommandLine(token, forceQuotes: true)));
    }

    /// <summary>
    /// Parses the executable token while retaining argument boundaries for CliWrap.
    /// </summary>
    public static ProcessCommand ParseProcessCommand(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            throw new ArgumentException("A process name or command is required.", nameof(commandLine));
        }

        if (commandLine.Length > MaximumCommandLineCharacters)
        {
            throw new ArgumentException(
                $"The process command cannot exceed {MaximumCommandLineCharacters} characters.", nameof(commandLine));
        }

        // An explicit command may be a single executable path containing spaces. Shell quoting
        // is not present in an argv value by the time this method runs, so recognize an existing
        // complete path before treating whitespace as the command/argument boundary.
        var completePath = commandLine.Trim();
        // NormalizeArguments starts an explicitly serialized argv command with a quote. Its quote
        // characters are syntax, never part of an ambient executable filename, so filesystem
        // probing must not reinterpret the serialized command as one complete or prefixed path.
        var isSerializedArgv = completePath.StartsWith("\"", StringComparison.Ordinal);
        var canProbeExplicitPath = !isSerializedArgv && IsExplicitPathCandidate(completePath);
        if (canProbeExplicitPath && File.Exists(completePath))
        {
            return new ProcessCommand(completePath, string.Empty);
        }

        // The outer shell removes quotes from `--command "path with spaces --arg"`. Recover the
        // executable boundary by choosing the longest existing file prefix before tokenizing, but
        // only when the command begins with an explicit path. Simple PATH names must never turn
        // into a different executable merely because a combined filename exists in the CWD.
        var firstNonWhitespace = canProbeExplicitPath
            ? commandLine.IndexOfAny([' ', '\t', '\r', '\n'])
            : -1;
        var longestPath = string.Empty;
        var longestPathIndex = -1;
        var pathProbeCount = 0;
        for (var boundary = firstNonWhitespace; boundary >= 0 && boundary < commandLine.Length &&
             pathProbeCount++ < MaximumExecutablePathProbes;)
        {
            while (boundary < commandLine.Length && !char.IsWhiteSpace(commandLine[boundary]))
            {
                boundary++;
            }

            var candidate = commandLine[..boundary].Trim();
            if (candidate.Length > 0 && File.Exists(candidate) && candidate.Length > longestPath.Length)
            {
                longestPath = candidate;
                longestPathIndex = boundary;
            }

            while (boundary < commandLine.Length && char.IsWhiteSpace(commandLine[boundary]))
            {
                boundary++;
            }

            var nextBoundary = commandLine.IndexOfAny([' ', '\t', '\r', '\n'], boundary);
            if (nextBoundary < 0)
            {
                break;
            }

            boundary = nextBoundary;
        }

        if (longestPathIndex >= 0)
        {
            var pathArguments = commandLine[longestPathIndex..].Trim();
            return new ProcessCommand(longestPath, NormalizeSingleQuotedArguments(pathArguments));
        }

        var processName = ParseFirstToken(commandLine, out var processEnd);
        var processArguments = processEnd < commandLine.Length
            ? commandLine[processEnd..].TrimStart()
            : string.Empty;
        return new ProcessCommand(processName, NormalizeSingleQuotedArguments(processArguments));
    }

    private static string ParseFirstToken(string commandLine, out int endIndex)
    {
        var index = 0;
        while (index < commandLine.Length && char.IsWhiteSpace(commandLine[index]))
        {
            index++;
        }

        var token = new StringBuilder();
        var tokenStarted = false;
        var quote = '\0';
        while (index < commandLine.Length)
        {
            var current = commandLine[index];
            if (quote == '\'')
            {
                if (current == '\'')
                {
                    quote = '\0';
                }
                else
                {
                    token.Append(current);
                }

                index++;
                continue;
            }

            if (quote == '"')
            {
                AppendDoubleQuotedCharacter(commandLine, token, ref index, ref quote);
                continue;
            }

            if (char.IsWhiteSpace(current))
            {
                if (tokenStarted)
                {
                    break;
                }

                index++;
                continue;
            }

            if (current == '"')
            {
                quote = current;
                tokenStarted = true;
                index++;
                continue;
            }

            if (current == '\'')
            {
                // Apostrophes at a token boundary, after an assignment, or after a quoted span
                // delimit a single-quoted value. Within an ordinary word they are literal data.
                if (token.Length == 0 || commandLine[index - 1] == '=' ||
                    commandLine[index - 1] == '"')
                {
                    quote = current;
                    tokenStarted = true;
                }
                else
                {
                    token.Append(current);
                    tokenStarted = true;
                }

                index++;
                continue;
            }

            if (current == '\\' && index + 1 < commandLine.Length &&
                commandLine[index + 1] == '\'')
            {
                token.Append(commandLine[++index]);
                tokenStarted = true;
                index++;
                continue;
            }

            if (current == '\\')
            {
                AppendDoubleQuotedCharacter(commandLine, token, ref index, ref quote);
                tokenStarted = true;
                continue;
            }

            token.Append(current);
            tokenStarted = true;
            index++;
        }

        if (quote != '\0')
        {
            throw new ArgumentException("The process command contains an unterminated quote.", nameof(commandLine));
        }

        if (!tokenStarted || token.Length == 0)
        {
            throw new ArgumentException("A process name is required.", nameof(commandLine));
        }

        endIndex = index;
        return token.ToString();
    }

    private static void AppendDoubleQuotedCharacter(
        string text,
        StringBuilder token,
        ref int index,
        ref char quote)
    {
        var current = text[index];
        if (current == '"')
        {
            quote = quote == '"' ? '\0' : '"';
            index++;
            return;
        }

        if (current != '\\')
        {
            token.Append(current);
            index++;
            return;
        }

        var runStart = index;
        while (index < text.Length && text[index] == '\\')
        {
            index++;
        }

        var count = index - runStart;
        if (index < text.Length && text[index] == '"')
        {
            token.Append('\\', count / 2);
            if ((count & 1) != 0)
            {
                token.Append('"');
            }
            else
            {
                quote = quote == '"' ? '\0' : '"';
            }

            index++;
            return;
        }

        token.Append('\\', count);
    }

    private static List<string> TokenizeCommandLine(string commandLine)
    {
        var tokens = new List<string>();
        var token = new StringBuilder();
        var tokenStarted = false;
        var quote = '\0';

        for (var index = 0; index < commandLine.Length; index++)
        {
            var current = commandLine[index];
            if (quote == '\'')
            {
                if (current == '\'')
                {
                    quote = '\0';
                }
                else
                {
                    token.Append(current);
                }

                continue;
            }

            if (quote == '"')
            {
                AppendDoubleQuotedCharacter(commandLine, token, ref index, ref quote);
                index--;
                continue;
            }

            if (char.IsWhiteSpace(current))
            {
                if (tokenStarted)
                {
                    tokens.Add(token.ToString());
                    token.Clear();
                    tokenStarted = false;
                }

                continue;
            }

            if (current == '"')
            {
                quote = current;
                tokenStarted = true;
                continue;
            }

            if (current == '\'')
            {
                if (token.Length == 0 || commandLine[index - 1] == '=' ||
                    commandLine[index - 1] == '"')
                {
                    quote = current;
                    tokenStarted = true;
                }
                else
                {
                    token.Append(current);
                    tokenStarted = true;
                }

                continue;
            }

            if (current == '\\' && index + 1 < commandLine.Length &&
                commandLine[index + 1] == '\'')
            {
                token.Append(commandLine[++index]);
                tokenStarted = true;
                continue;
            }

            if (current == '\\')
            {
                AppendDoubleQuotedCharacter(commandLine, token, ref index, ref quote);
                index--;
                tokenStarted = true;
                continue;
            }

            token.Append(current);
            tokenStarted = true;
        }

        if (quote != '\0')
        {
            throw new ArgumentException("The process command contains an unterminated quote.", nameof(commandLine));
        }

        if (tokenStarted)
        {
            tokens.Add(token.ToString());
        }

        return tokens;
    }

    private static string NormalizeSingleQuotedArguments(string text)
    {
        if (!text.Contains('\'', StringComparison.Ordinal))
        {
            return text;
        }

        // Once both quote styles occur in the same argument text, canonicalize the parsed values
        // before escaping them. This handles POSIX concatenation such as `'foo'"'"'bar'` without
        // leaving adjacent generated double-quote delimiters that CliWrap would interpret as
        // literal quotes.
        if (text.Contains('"', StringComparison.Ordinal))
        {
            return string.Join(" ", TokenizeCommandLine(text).Select(token => QuoteTokenForCommandLine(token)));
        }

        var builder = new StringBuilder(text.Length);
        var inSingleQuote = false;
        var inDoubleQuote = false;
        var backslashRun = 0;
        for (var index = 0; index < text.Length; index++)
        {
            var current = text[index];
            if (inSingleQuote)
            {
                if (current == '\'')
                {
                    // The closing quote follows the literal content. Double a trailing run so
                    // CliWrap does not consume the generated delimiter as an escaped quote.
                    if (backslashRun > 0)
                    {
                        builder.Append('\\', backslashRun);
                    }

                    inSingleQuote = false;
                    backslashRun = 0;
                    builder.Append('"');
                }
                else if (current == '"')
                {
                    // A literal quote inside a POSIX single-quoted segment needs escaping after
                    // conversion to a CliWrap double-quoted segment.
                    builder.Append('\\', backslashRun + 1);
                    builder.Append('"');
                    backslashRun = 0;
                }
                else
                {
                    builder.Append(current);
                    backslashRun = current == '\\' ? backslashRun + 1 : 0;
                }

                continue;
            }

            if (inDoubleQuote)
            {
                builder.Append(current);
                if (current == '"' && (index == 0 || text[index - 1] != '\\'))
                {
                    inDoubleQuote = false;
                }
                else if (current == '\\' && index + 1 < text.Length &&
                         (text[index + 1] == '"' || text[index + 1] == '\\'))
                {
                    builder.Append(text[++index]);
                }

                continue;
            }

            if (current == '"')
            {
                inDoubleQuote = true;
                builder.Append(current);
                continue;
            }

            if (current == '\'' &&
                (index == 0 || char.IsWhiteSpace(text[index - 1]) || text[index - 1] == '=' ||
                 text[index - 1] == '"'))
            {
                inSingleQuote = true;
                backslashRun = 0;
                builder.Append('"');
            }
            else
            {
                builder.Append(current);
            }
        }

        if (inSingleQuote || inDoubleQuote)
        {
            throw new ArgumentException("The process command contains an unterminated quote.", nameof(text));
        }

        return builder.ToString();
    }

    private static string JoinArgumentValues(IEnumerable<string> arguments)
    {
        return string.Join(" ", arguments.Select(token => QuoteTokenForCommandLine(token, forceQuotes: true)));
    }

    private static bool IsExplicitPathCandidate(string value)
    {
        var candidate = value.TrimStart();
        return Path.IsPathRooted(candidate) ||
               candidate.StartsWith("./", StringComparison.Ordinal) ||
               candidate.StartsWith("../", StringComparison.Ordinal) ||
               candidate.StartsWith(".\\", StringComparison.Ordinal) ||
               candidate.StartsWith("..\\", StringComparison.Ordinal) ||
               candidate.StartsWith("\\\\", StringComparison.Ordinal) ||
               (candidate.Length >= 2 && candidate[1] == ':' &&
                (candidate[0] is >= 'A' and <= 'Z' or >= 'a' and <= 'z'));
    }

    /// <summary>
    /// Returns whether a path should be treated as a configuration path in legacy mode.
    /// </summary>
    public static bool IsConfigurationPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var candidate = path.Trim();
        var hasJsonExtension = string.Equals(Path.GetExtension(candidate), ".json", StringComparison.OrdinalIgnoreCase);
        if (hasJsonExtension)
        {
            // A .json primary argument is configuration even when it is missing. Config.LoadConfiguration
            // then reports a clear FileNotFoundException instead of trying to execute it as a process.
            return true;
        }

        return File.Exists(candidate) && LooksLikeJsonConfiguration(candidate);
    }

    /// <summary>
    /// Checks the first non-whitespace byte of a file for a JSON object. This is only an inference
    /// for legacy, extensionless paths; explicit --config remains authoritative for any JSON value.
    /// </summary>
    public static bool LooksLikeJsonConfiguration(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            var buffer = new byte[4096];
            var read = stream.Read(buffer, 0, buffer.Length);
            var index = 0;
            if (read >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
            {
                index = 3;
            }

            while (index < read && char.IsWhiteSpace((char)buffer[index]))
            {
                index++;
            }

            return index < read && buffer[index] == (byte)'{';
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string QuoteTokenForCommandLine(string token, bool forceQuotes = false)
    {
        if (token.Length == 0)
        {
            return "\"\"";
        }

        var requiresQuotes = forceQuotes || token.Any(character => char.IsWhiteSpace(character) || character is '"' or '\'');
        if (!requiresQuotes)
        {
            return token;
        }

        var builder = new StringBuilder(token.Length + 2);
        builder.Append('"');
        var backslashes = 0;
        foreach (var current in token)
        {
            if (current == '\\')
            {
                backslashes++;
                continue;
            }

            if (current == '"')
            {
                builder.Append('\\', (backslashes * 2) + 1);
                builder.Append('"');
            }
            else
            {
                builder.Append('\\', backslashes);
                builder.Append(current);
            }

            backslashes = 0;
        }

        // Backslashes immediately before a closing quote must be doubled so they remain literal.
        builder.Append('\\', backslashes * 2);
        builder.Append('"');
        return builder.ToString();
    }
}

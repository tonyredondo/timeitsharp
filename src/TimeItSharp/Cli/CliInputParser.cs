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

        // File.Exists must be checked against the complete argument before tokenizing. A path
        // containing spaces is one shell argument, while "echo hello.json" is a command line.
        // This preserves existing configuration paths with spaces without making a suffix in an
        // argument select configuration mode.
        var completeValue = argumentValue.Trim();
        if (File.Exists(completeValue) && IsConfigurationPath(completeValue))
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

        if (separator < 0)
        {
            return args.ToArray();
        }

        var normalized = new List<string>(separator + 2);
        for (var index = 0; index < separator; index++)
        {
            normalized.Add(args[index]);
        }

        var commandTokens = args.Skip(separator + 1).ToArray();
        // A command passed as one quoted shell argument is already a complete command line and
        // must not receive another pair of quotes ("echo hello.json" would otherwise become one
        // executable token). Multiple argv tokens are joined while quoting tokens that contain
        // spaces so their boundaries survive the second parser.
        var command = JoinCommandArguments(commandTokens);
        // Use the equals form so a command whose executable starts with '-' is still consumed as
        // the option value rather than being parsed as another System.CommandLine option. The
        // value is already one argv item, so spaces do not need shell-level quoting here.
        normalized.Add($"--command={command}");
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

        // Some callers pass the legacy complete command string as the first argv value and
        // append process arguments after the option terminator. Do not quote that value as an
        // executable unless it is an existing path (where whitespace really belongs to the path).
        // This keeps both `-- "echo hello.json" --flag` and `-- echo hello.json --flag` useful.
        var first = arguments[0];
        var firstTrimmed = first.Trim();
        var looksLikePath = Path.IsPathRooted(firstTrimmed) ||
                            firstTrimmed.StartsWith("./", StringComparison.Ordinal) ||
                            firstTrimmed.StartsWith("../", StringComparison.Ordinal) ||
                            firstTrimmed.StartsWith(".\\", StringComparison.Ordinal) ||
                            (firstTrimmed.Length >= 2 && firstTrimmed[1] == ':' &&
                             (firstTrimmed[0] is >= 'A' and <= 'Z' or >= 'a' and <= 'z'));
        if (first.Any(char.IsWhiteSpace) && !File.Exists(firstTrimmed) && !looksLikePath)
        {
            var remainder = JoinCommandArguments(arguments.Skip(1).ToArray());
            return string.IsNullOrEmpty(remainder) ? first : $"{first} {remainder}";
        }

        return string.Join(" ", arguments.Select(QuoteTokenForCommandLine));
    }

    /// <summary>
    /// Parses the executable token while retaining the original argument text for CliWrap.
    /// </summary>
    public static ProcessCommand ParseProcessCommand(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            throw new ArgumentException("A process name or command is required.", nameof(commandLine));
        }

        // An explicit command may be a single executable path containing spaces. Shell quoting
        // is not present in an argv value by the time this method runs, so recognize an existing
        // complete path before treating whitespace as the command/argument boundary.
        var completePath = commandLine.Trim();
        if (File.Exists(completePath))
        {
            return new ProcessCommand(completePath, string.Empty);
        }

        var index = 0;
        while (index < commandLine.Length && char.IsWhiteSpace(commandLine[index]))
        {
            index++;
        }

        var processName = new StringBuilder();
        char quote = '\0';
        while (index < commandLine.Length)
        {
            var current = commandLine[index];
            if (quote != '\0')
            {
                if (current == quote)
                {
                    quote = '\0';
                    index++;
                    continue;
                }

                // Preserve normal backslashes (important for Windows paths), while accepting the
                // conventional escaped quote and escaped backslash forms.
                if (current == '\\' && index + 1 < commandLine.Length &&
                    (commandLine[index + 1] == quote || commandLine[index + 1] == '\\'))
                {
                    processName.Append(commandLine[index + 1]);
                    index += 2;
                    continue;
                }

                processName.Append(current);
                index++;
                continue;
            }

            if (char.IsWhiteSpace(current))
            {
                break;
            }

            if (current == '"' ||
                (current == '\'' && (index == 0 || char.IsWhiteSpace(commandLine[index - 1]))))
            {
                quote = current;
                index++;
                continue;
            }

            // An apostrophe inside an unquoted word is ordinary data (for example, "don't").
            if (current == '\'')
            {
                processName.Append(current);
                index++;
                continue;
            }

            if (current == '\\' && index + 1 < commandLine.Length &&
                (commandLine[index + 1] is '\'' or '"'))
            {
                processName.Append(commandLine[index + 1]);
                index += 2;
                continue;
            }

            processName.Append(current);
            index++;
        }

        if (quote != '\0')
        {
            throw new ArgumentException("The process command contains an unterminated quote.", nameof(commandLine));
        }

        if (processName.Length == 0)
        {
            throw new ArgumentException("A process name is required.", nameof(commandLine));
        }

        while (index < commandLine.Length && char.IsWhiteSpace(commandLine[index]))
        {
            index++;
        }

        var processArguments = index < commandLine.Length ? commandLine[index..].TrimEnd() : string.Empty;
        EnsureBalancedQuotes(processArguments, commandLine);
        return new ProcessCommand(processName.ToString(), processArguments);
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

    private static string QuoteTokenForCommandLine(string token)
    {
        if (token.Length == 0)
        {
            return "\"\"";
        }

        if (token.All(character => !char.IsWhiteSpace(character) && character != '\"'))
        {
            return token;
        }

        return $"\"{token.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }

    private static void EnsureBalancedQuotes(string text, string commandLine)
    {
        char quote = '\0';
        for (var index = 0; index < text.Length; index++)
        {
            var current = text[index];
            if (current == '\\' && quote == '"' && index + 1 < text.Length &&
                (text[index + 1] == quote || text[index + 1] == '\\'))
            {
                index++;
                continue;
            }

            if (quote == '\0' && current == '"')
            {
                quote = current;
            }
            else if (quote == '\0' && current == '\'' &&
                     (index == 0 || char.IsWhiteSpace(text[index - 1])))
            {
                quote = current;
            }
            else if (quote != '\0' && current == quote)
            {
                quote = '\0';
            }
        }

        if (quote != '\0')
        {
            throw new ArgumentException("The process command contains an unterminated quote.", nameof(commandLine));
        }
    }
}

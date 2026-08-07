using System.Text;

namespace TimeItSharp.Common;

/// <summary>
/// Parses the command-argument grammar used by configuration, timeout, and callback commands.
/// CliWrap's string overload is platform-specific and treats the text as an already escaped
/// command line. Parsing once and passing individual values prevents spaces, quotes, and trailing
/// backslashes from changing argument boundaries on the target platform.
/// </summary>
internal static class CommandLineArguments
{
    private const int MaximumArgumentTextLength = 1024 * 1024;
    private const int MaximumArgumentCount = 4096;

    internal static IReadOnlyList<string> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        if (text.Length > MaximumArgumentTextLength)
        {
            throw new ArgumentException(
                $"Process arguments cannot exceed {MaximumArgumentTextLength} characters.", nameof(text));
        }

        var arguments = new List<string>();
        var value = new StringBuilder();
        var quote = '\0';
        var tokenStarted = false;

        for (var index = 0; index < text.Length; index++)
        {
            var current = text[index];
            if (quote == '\'')
            {
                if (current == '\'')
                {
                    quote = '\0';
                }
                else
                {
                    value.Append(current);
                }

                continue;
            }

            if (quote == '"')
            {
                if (current == '\\')
                {
                    AppendBackslashRun(text, ref index, value, ref quote);
                }
                else if (current == '"')
                {
                    quote = '\0';
                }
                else
                {
                    value.Append(current);
                }

                continue;
            }

            if (char.IsWhiteSpace(current))
            {
                AddTokenIfStarted(arguments, value, ref tokenStarted);
                continue;
            }

            tokenStarted = true;
            if (current == '"')
            {
                quote = current;
            }
            else if (current == '\'')
            {
                if (ShouldStartSingleQuote(text, index, value.Length == 0))
                {
                    quote = current;
                }
                else
                {
                    // Apostrophes in ordinary words are data, not quote delimiters. This keeps
                    // contractions and names such as "don't" and "O'Brien" intact even when a
                    // later argument happens to be single quoted.
                    value.Append(current);
                }
            }
            else if (current == '\\')
            {
                AppendBackslashRun(text, ref index, value, ref quote);
            }
            else
            {
                value.Append(current);
            }
        }

        if (quote != '\0')
        {
            throw new ArgumentException("The process arguments contain an unterminated quote.", nameof(text));
        }

        AddTokenIfStarted(arguments, value, ref tokenStarted);
        return arguments;
    }

    private static void AppendBackslashRun(
        string text,
        ref int index,
        StringBuilder value,
        ref char quote)
    {
        var start = index;
        while (index < text.Length && text[index] == '\\')
        {
            index++;
        }

        var count = index - start;
        if (index >= text.Length)
        {
            value.Append('\\', count);
            index--;
            return;
        }

        var next = text[index];
        if (next == '"')
        {
            // Match the CommandLineToArgvW/CRT convention used by QuoteTokenForCommandLine:
            // pairs become literal backslashes; an odd remainder escapes the quote.
            value.Append('\\', count / 2);
            if ((count & 1) != 0)
            {
                value.Append('"');
            }
            else
            {
                quote = quote == '"' ? '\0' : '"';
            }

            return;
        }

        value.Append('\\', count);
        index--;
    }

    private static bool ShouldStartSingleQuote(string text, int index, bool atTokenBoundary)
    {
        // Embedded apostrophes are always literal. Single-quote grouping starts only in an
        // unambiguous quote position; this preserves adjacent contractions and possessives.
        return atTokenBoundary ||
               (index > 0 && (text[index - 1] == '=' || text[index - 1] == '"'));
    }

    private static void AddTokenIfStarted(List<string> arguments, StringBuilder value, ref bool tokenStarted)
    {
        if (!tokenStarted)
        {
            return;
        }

        if (arguments.Count >= MaximumArgumentCount)
        {
            throw new ArgumentException(
                $"Process arguments cannot contain more than {MaximumArgumentCount} values.");
        }

        arguments.Add(value.ToString());
        value.Clear();
        tokenStarted = false;
    }
}

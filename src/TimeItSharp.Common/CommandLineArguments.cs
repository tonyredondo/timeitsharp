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

            if (quote == '\"')
            {
                if (current == '\"')
                {
                    quote = '\0';
                }
                else if (current == '\\' && index + 1 < text.Length &&
                         (text[index + 1] == '\"' || text[index + 1] == '\\'))
                {
                    value.Append(text[++index]);
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
            if (current == '\"')
            {
                quote = current;
            }
            else if (current == '\'')
            {
                var closingQuote = text.IndexOf('\'', index + 1);
                var closingQuoteIsBoundary = closingQuote >= 0 &&
                    (closingQuote + 1 >= text.Length || char.IsWhiteSpace(text[closingQuote + 1]) ||
                     text[closingQuote + 1] is ',' or ';');
                var startsQuote = index == 0 || char.IsWhiteSpace(text[index - 1]) ||
                                  text[index - 1] is '=' or '"' || closingQuoteIsBoundary;
                if (startsQuote)
                {
                    if (closingQuote < 0 && index > 0 && text[index - 1] == '=')
                    {
                        throw new ArgumentException("The process arguments contain an unterminated quote.", nameof(text));
                    }

                    quote = current;
                }
                else
                {
                    value.Append(current);
                }
            }
            else if (current == '\\' && index + 1 < text.Length &&
                     (text[index + 1] == '\"' || text[index + 1] == '\'' ||
                      char.IsWhiteSpace(text[index + 1])))
            {
                value.Append(text[++index]);
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

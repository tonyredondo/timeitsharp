using Spectre.Console;
using System.Text;

namespace TimeItSharp.Common;

public sealed class TemplateVariables
{
    private const int MaximumVariableEntries = 1_024;
    private const int MaximumVariableTextLength = 64 * 1024;
    private const int MaximumExpandedTextLength = 64 * 1024;
    private static readonly string VariableOpen = "$(";
    private static readonly string VariableClose = ")";

    private readonly Dictionary<string, string> _variables = new();

    public TemplateVariables()
    {
        // default one
        _variables.Add(CreateVariable("CWD"), Environment.CurrentDirectory);
    }

    public int Length => _variables.Count;

    // Exporters use this read-only view to identify values introduced by sensitive template
    // variables. Keeping the dictionary private preserves the builder API.
    internal IEnumerable<KeyValuePair<string, string>> EntriesForSanitization => _variables;

    public void Add(string name, string value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A template variable name is required.", nameof(name));
        }
        if (name.Length > MaximumVariableTextLength)
        {
            throw new ArgumentException($"A template variable name cannot exceed {MaximumVariableTextLength} characters.", nameof(name));
        }
        if (value.Length > MaximumVariableTextLength)
        {
            throw new ArgumentException($"A template variable value cannot exceed {MaximumVariableTextLength} characters.", nameof(value));
        }

        var key = CreateVariable(name);
        if (!_variables.ContainsKey(key) && _variables.Count >= MaximumVariableEntries)
        {
            throw new ArgumentException($"No more than {MaximumVariableEntries} template variables are supported.", nameof(name));
        }

        if (!_variables.TryAdd(key, value))
        {
            AnsiConsole.MarkupLine(
                "[bold red] This variable '{0}' already exists.[/]",
                Utils.EscapeMarkup(Utils.SanitizeText(name)));
        }
    }

    public string Expand(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return s;

        if (s.Length > MaximumExpandedTextLength)
        {
            throw new ArgumentException($"Expanded template text cannot exceed {MaximumExpandedTextLength} characters.", nameof(s));
        }

        if (!s.Contains(VariableOpen))
            return s;

        var sb = new StringBuilder(s);
        foreach (var (k, v) in _variables)
        {
            var current = sb.ToString();
            var occurrences = CountOccurrences(current, k);
            var projectedLength = (long)current.Length + (long)occurrences * (v.Length - k.Length);
            if (projectedLength > MaximumExpandedTextLength)
            {
                throw new ArgumentException($"Expanded template text cannot exceed {MaximumExpandedTextLength} characters.", nameof(s));
            }

            sb.Replace(k, v);
        }

        return sb.ToString();

        static int CountOccurrences(string text, string value)
        {
            var count = 0;
            var index = 0;
            while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }

            return count;
        }
    }

    private static string CreateVariable(string name)
    {
        return $"{VariableOpen}{name}{VariableClose}";
    }

    public TemplateVariables Clone()
    {
        var tempVars = new TemplateVariables();
        foreach (var variable in _variables)
        {
            tempVars._variables[variable.Key] = variable.Value;
        }

        return tempVars;
    }
}

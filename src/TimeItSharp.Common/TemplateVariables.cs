﻿using Spectre.Console;
using System.Text;

namespace TimeItSharp.Common;

public sealed class TemplateVariables
{
    private static readonly string VariableOpen = "$(";
    private static readonly string VariableClose = ")";

    private readonly Dictionary<string, string> _variables = new();

    public TemplateVariables()
    {
        // default one
        _variables.Add(CreateVariable("CWD"), Environment.CurrentDirectory);
    }

    public int Length => _variables.Count;

    public void Add(string name, string value)
    {
        var key = CreateVariable(name);
        if (!_variables.TryAdd(key, value))
        {
            AnsiConsole.MarkupLine("[bold red] This variable '{0}' already exists.[/]", name);
        }
    }

    public string Expand(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return s;

        if (!s.Contains(VariableOpen))
            return s;

        var sb = new StringBuilder(s);
        foreach( var (k, v) in _variables)
        {
            sb.Replace(k, v);
        }

        return sb.ToString();
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

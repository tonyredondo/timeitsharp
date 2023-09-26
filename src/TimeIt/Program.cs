using Spectre.Console;
using TimeIt.Common;
using System.CommandLine;

var version = typeof(Program).Assembly.GetName().Version!;
AnsiConsole.MarkupLine("[bold dodgerblue1 underline]TimeIt v{0}[/]", $"{version.Major}.{version.Minor}.{version.Build}");

var argument = new Argument<string>("configuration file", "The JSON configuration file");
var templateVariables = new Option<TemplateVariables>(
    "--variable",
    isDefault: true,
    description: "Variables used to instantiate the configuration file",
    parseArgument: result =>
    {
        var tvs = new TemplateVariables();

        foreach (var token in result.Tokens)
        {
            var variableValue = token.Value; ;
            var idx = variableValue.IndexOf('=');
            if (idx == -1)
            {
                AnsiConsole.MarkupLine("[bold red]Unknown format: variable must be of the form[/][bold blue] key=value[/]");
                continue;
            }

            if (idx == variableValue.Length - 1)
            {
                AnsiConsole.MarkupLine("[bold red]No variable value provided. Skipped.[/]");
                continue;
            }

            if (idx == 0)
            {
                AnsiConsole.MarkupLine("[bold red]No variable name provided. Skipped.[/]");
                continue;
            }

            var keyVal = variableValue.Split('=');
            tvs.Add(keyVal[0], keyVal[1]);
        }
        return tvs;

    }) { Arity = ArgumentArity.OneOrMore };

var root = new RootCommand
{
    argument,
    templateVariables
};

root.SetHandler(async (configFile, templateVariables) =>
{
    var exitCode = await TimeItEngine.RunAsync(configFile, templateVariables).ConfigureAwait(false);
    if (exitCode != 0)
    {
        Environment.Exit(exitCode);
    }
}, argument, templateVariables);

await root.InvokeAsync(args);

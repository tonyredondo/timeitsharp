﻿using Spectre.Console;
using TimeIt;
using TimeIt.Common.Configuration;
using TimeIt.Common.Exporter;
using TimeIt.Common.Results;
using TimeIt.DatadogExporter;
using System.CommandLine;

AnsiConsole.MarkupLine("[bold dodgerblue1 underline]TimeIt v{0}[/]", GetVersion());

var argument = new Argument<string>("configuration file", "The JSON configuration file");
var templateVariables = new Option<TemplateVariables>("--variable", isDefault: true, description: "Variables used to instantiate the configuration file",
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
    // Load configuration
    var config = Config.LoadConfiguration(configFile);
    config.JsonExporterFilePath = templateVariables.Expand(config.JsonExporterFilePath);

    // Create scenario processor
    var processor = new ScenarioProcessor(config, templateVariables);

    // Enable exporters
    var exporters = new List<IExporter>();
    exporters.Add(new ConsoleExporter());
    exporters.Add(new JsonExporter());
    exporters.Add(new TimeItDatadogExporter());

    AnsiConsole.MarkupLine("[bold aqua]Warmup count:[/] {0}", config.WarmUpCount);
    AnsiConsole.MarkupLine("[bold aqua]Count:[/] {0}", config.Count);
    AnsiConsole.MarkupLine("[bold aqua]Number of Scenarios:[/] {0}", config.Scenarios.Count);
    AnsiConsole.MarkupLine("[bold aqua]Exporters:[/] {0}", string.Join(", ", exporters.Select(e => e.Name)));
    AnsiConsole.WriteLine();

    // Process scenarios
    var scenariosResults = new List<ScenarioResult>();
    var scenarioWithErrors = 0;
    if (config is { Count: > 0, Scenarios.Count: > 0 })
    {
        foreach (var scenario in config.Scenarios)
        {
            // Prepare scenario
            processor.PrepareScenario(scenario);

            // Process scenario
            var result = await processor.ProcessScenarioAsync(scenario).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(result.Error))
            {
                scenarioWithErrors++;
            }

            scenariosResults.Add(result);
        }

        if (scenarioWithErrors < config.Scenarios.Count)
        {
            // Export data
            foreach (var exporter in exporters)
            {
                exporter.SetConfiguration(config);
                if (exporter.Enabled)
                {
                    exporter.Export(scenariosResults);
                }
            }

            // Clean scenarios
            foreach (var scenario in config.Scenarios)
            {
                processor.CleanScenario(scenario);
            }
        }
        else
        {
            AnsiConsole.WriteLine();

            for (var i = 0; i < scenariosResults.Count; i++)
            {
                if (!string.IsNullOrEmpty(scenariosResults[i].Error))
                {
                    AnsiConsole.MarkupLine("[red]Error in Scenario[/]: {0}", scenariosResults[i].Name);
                    AnsiConsole.WriteLine(scenariosResults[i].Error);
                }
            }

            // Clean scenarios
            foreach (var scenario in config.Scenarios)
            {
                processor.CleanScenario(scenario);
            }

            Environment.Exit(1);
        }
    }
}, argument, templateVariables);


await root.InvokeAsync(args);

return;

static string GetVersion()
{
    var version = typeof(Utils).Assembly.GetName().Version;
    if (version is null)
    {
        return "unknown";
    }

    return $"{version.Major}.{version.Minor}.{version.Build}";
}

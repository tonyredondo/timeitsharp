using Spectre.Console;
using TimeIt;
using TimeIt.Common.Configuration;
using TimeIt.Common.Exporter;
using TimeIt.Common.Results;
using TimeIt.DatadogExporter;

AnsiConsole.MarkupLine("[bold dodgerblue1 underline]TimeIt (v. {0}) by Tony Redondo[/]\n", typeof(Utils).Assembly.GetName().Version?.ToString() ?? "unknown");

// Check arguments
if (args.Length < 1)
{
    AnsiConsole.MarkupLine("[red]Missing argument with the configuration file.[/]");
    Environment.Exit(1);
    return;
}

// Load configuration
var config = Config.LoadConfiguration(args[0]);
config.JsonExporterFilePath = Utils.ReplaceCustomVars(config.JsonExporterFilePath);

// Create scenario processor
var processor = new ScenarioProcessor(config);

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
	    for (var i = 0; i < scenariosResults.Count; i++)
	    {
		    if (!string.IsNullOrEmpty(scenariosResults[i].Error))
		    {
			    AnsiConsole.MarkupLine("Error in Scenario: {0}", i);
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

using System.Runtime.Loader;
using CliWrap;
using CliWrap.Buffered;
using Spectre.Console;
using TimeIt;
using TimeIt.Common.Configuration;
using TimeIt.Common.Exporter;
using TimeIt.Common.Results;

AnsiConsole.MarkupLine("[bold aqua]TimeIt by Tony Redondo[/]\n");

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

// Enable exporters
var exporters = new List<IExporter>();
exporters.Add(new JsonExporter());

AnsiConsole.MarkupLine("Warmup count: {0}", config.WarmUpCount);
AnsiConsole.MarkupLine("Count: {0}", config.WarmUpCount);
AnsiConsole.MarkupLine("Number of Scenarios: {0}", config.Scenarios.Count);
AnsiConsole.MarkupLine("Exporters: {0}", string.Join(", ", exporters.Select(e => e.Name)));

var scenariosResults = new List<ScenarioResult>();
var scenarioWithErrors = 0;

return;

for (var i = 0; i < 10; i++)
{
    var result = await Cli.Wrap("ls")
        .ExecuteBufferedAsync()
        .ConfigureAwait(false);

    // Console.WriteLine(result.StandardOutput);
    Console.WriteLine(result.RunTime.TotalMilliseconds);
}


Console.WriteLine(config);
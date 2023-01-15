using System.Text.Json;
using Spectre.Console;
using TimeIt.Common.Configuration;
using TimeIt.Common.Exporter;
using TimeIt.Common.Results;

namespace TimeIt;

public class JsonExporter : IExporter
{
    private Config? _configuration;

    public string Name => "JsonExporter";
    
    public bool Enabled => true;

    public void SetConfiguration(Config configuration)
    {
        _configuration = configuration;
    }

    public void Export(IEnumerable<ScenarioResult> results)
    {
        try
        {
            var outputFile = _configuration?.JsonExporterFilePath ?? string.Empty;
            if (string.IsNullOrEmpty(outputFile))
            {
                outputFile = Path.Combine(Environment.CurrentDirectory, $"jsonexporter_{Random.Shared.Next()}.json");
            }

            using var fStream = File.OpenWrite(outputFile);
            JsonSerializer.Serialize(fStream, results, new JsonSerializerOptions(JsonSerializerDefaults.General)
            {
                WriteIndented = true
            });
            
            AnsiConsole.MarkupLine($"[lime]The json file '{outputFile}' was exported.[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Error exporting to json:[/]");
            AnsiConsole.WriteException(ex);
        }
    }
}
using System.Text.Json;
using Spectre.Console;
using TimeItSharp.Common.Results;

namespace TimeItSharp.Common.Exporters;

public sealed class JsonExporter : IExporter
{
    private InitOptions _options;

    public string Name => nameof(JsonExporter);
    
    public bool Enabled => true;

    public void Initialize(InitOptions options)
    {
        _options = options;
    }

    public void Export(IEnumerable<ScenarioResult> results)
    {
        try
        {
            var outputFile = _options.Configuration?.JsonExporterFilePath ?? string.Empty;
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
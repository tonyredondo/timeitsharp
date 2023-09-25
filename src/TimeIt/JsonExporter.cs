using System.Text.Json;
using Spectre.Console;
using TimeIt.Common.Configuration;
using TimeIt.Common.Exporters;
using TimeIt.Common.Results;

namespace TimeIt;

public class JsonExporter : IExporter
{
    private Config? _configuration;

    public string Name => nameof(JsonExporter);
    
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
#if NET5_0
                outputFile = Path.Combine(Environment.CurrentDirectory, $"jsonexporter_{new Random().Next()}.json");
#else
                outputFile = Path.Combine(Environment.CurrentDirectory, $"jsonexporter_{Random.Shared.Next()}.json");
#endif
            }

            using var fStream = File.OpenWrite(outputFile);
#if NET5_0
            var utf8writer = new Utf8JsonWriter(fStream, new JsonWriterOptions
            {
                Indented = true
            });
            JsonSerializer.Serialize(utf8writer, results, new JsonSerializerOptions(JsonSerializerDefaults.General)
            {
                WriteIndented = true
            });
#else
            JsonSerializer.Serialize(fStream, results, new JsonSerializerOptions(JsonSerializerDefaults.General)
            {
                WriteIndented = true
            });
#endif

            
            AnsiConsole.MarkupLine($"[lime]The json file '{outputFile}' was exported.[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Error exporting to json:[/]");
            AnsiConsole.WriteException(ex);
        }
    }
}
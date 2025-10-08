using System.Diagnostics.CodeAnalysis;
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

    public void Export(TimeitResult results)
    {
        try
        {
            var outputFile = _options.Configuration?.JsonExporterFilePath ?? string.Empty;
            if (string.IsNullOrEmpty(outputFile))
            {
                outputFile = Path.Combine(Environment.CurrentDirectory, $"jsonexporter_{Random.Shared.Next()}.json");
            }

            foreach (var scenarioResult in results.Scenarios)
            {
                var tags = new Dictionary<string, object>(scenarioResult.Tags.Count);
                // Expanding custom tags
                foreach (var tag in scenarioResult.Tags)
                {
                    var key = _options.TemplateVariables.Expand(tag.Key);
                    if (tag.Value is string strValue)
                    {
                        tags[key] = _options.TemplateVariables.Expand(strValue);
                    }
                    else
                    {
                        tags[key] = tag.Value;
                    }
                }

                scenarioResult.Tags = tags;
            }

            using var fStream = File.OpenWrite(outputFile);
            JsonSerializer.Serialize(fStream, results.Scenarios, TimeItResultContext.Default.IReadOnlyListScenarioResult);
            AnsiConsole.MarkupLine($"[lime]The json file '{outputFile}' was exported.[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Error exporting to json:[/]");
#if AOT
            AnsiConsole.MarkupLine("[red]{0}[/]", ex.Message);
            AnsiConsole.WriteLine(ex.ToString());
#else
            AnsiConsole.WriteException(ex);
#endif
        }
    }
}
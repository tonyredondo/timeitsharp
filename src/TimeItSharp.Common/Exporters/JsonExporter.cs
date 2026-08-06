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

            IReadOnlyList<ScenarioResult> exportScenarios = results.Scenarios
                .Select(CreateExportScenarioResult)
                .ToList();

            var fullOutputFile = Path.GetFullPath(outputFile);
            var outputDirectory = Path.GetDirectoryName(fullOutputFile);
            if (!string.IsNullOrEmpty(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            using var fStream = File.Create(fullOutputFile);
            JsonSerializer.Serialize(fStream, exportScenarios, TimeItResultContext.Default.IReadOnlyListScenarioResult);
            AnsiConsole.MarkupLine($"[lime]The json file '{fullOutputFile}' was exported.[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Error exporting to json:[/]");
            AnsiConsole.WriteLine(ex.ToString());
            throw;
        }
    }

    private ScenarioResult CreateExportScenarioResult(ScenarioResult source)
    {
        var tags = new Dictionary<string, object>(source.Tags.Count);
        foreach (var tag in source.Tags)
        {
            var key = _options.TemplateVariables.Expand(tag.Key);
            if (tag.Value is string stringValue)
            {
                tags[key] = _options.TemplateVariables.Expand(stringValue);
            }
            else
            {
                tags[key] = tag.Value;
            }
        }

        var environmentVariables = new Dictionary<string, string>(source.EnvironmentVariables.Count);
        foreach (var environmentVariable in source.EnvironmentVariables)
        {
            environmentVariables[environmentVariable.Key] =
                Utils.IsSensitiveEnvironmentVariable(environmentVariable.Key)
                    ? "[REDACTED]"
                    : environmentVariable.Value;
        }

        return new ScenarioResult
        {
            Scenario = source.Scenario,
            Name = source.Name,
            IsBaseline = source.IsBaseline,
            ParentService = null,
            ProcessName = source.ProcessName,
            ProcessArguments = source.ProcessArguments,
            WorkingDirectory = source.WorkingDirectory,
            EnvironmentVariables = environmentVariables,
            PathValidations = new List<string>(source.PathValidations),
            Timeout = source.Timeout.Clone(),
            Tags = tags,
            Start = source.Start,
            End = source.End,
            Duration = source.Duration,
            Error = source.Error,
            WarmUpCount = source.WarmUpCount,
            Count = source.Count,
            Data = source.Data.ToList(),
            Durations = source.Durations.ToList(),
            Outliers = source.Outliers.ToList(),
            Mean = source.Mean,
            Median = source.Median,
            Max = source.Max,
            Min = source.Min,
            Stdev = source.Stdev,
            StdErr = source.StdErr,
            P99 = source.P99,
            P95 = source.P95,
            P90 = source.P90,
            Ci99 = source.Ci99.ToArray(),
            Ci95 = source.Ci95.ToArray(),
            Ci90 = source.Ci90.ToArray(),
            IsBimodal = source.IsBimodal,
            PeakCount = source.PeakCount,
            Metrics = new Dictionary<string, double>(source.Metrics),
            MetricsData = source.MetricsData.ToDictionary(
                item => item.Key,
                item => item.Value.ToList()),
            AdditionalMetrics = new Dictionary<string, double>(source.AdditionalMetrics),
            Status = source.Status,
            OutliersThreshold = source.OutliersThreshold,
            LastStandardOutput = source.LastStandardOutput,
        };
    }
}

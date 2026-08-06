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
            else if (tag.Value is double tagDouble && !double.IsFinite(tagDouble))
            {
                // Non-finite values are not valid JSON numbers. A custom callback must not make
                // the complete export fail just because one optional tag is malformed.
                continue;
            }
            else if (tag.Value is float tagFloat && !float.IsFinite(tagFloat))
            {
                continue;
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
            Data = source.Data.Select(CreateExportDataPoint).ToList(),
            Durations = source.Durations.Where(double.IsFinite).ToList(),
            Outliers = source.Outliers.Where(double.IsFinite).ToList(),
            Mean = FiniteOrZero(source.Mean),
            Median = FiniteOrZero(source.Median),
            Max = FiniteOrZero(source.Max),
            Min = FiniteOrZero(source.Min),
            Stdev = FiniteOrZero(source.Stdev),
            StdErr = FiniteOrZero(source.StdErr),
            P99 = FiniteOrZero(source.P99),
            P95 = FiniteOrZero(source.P95),
            P90 = FiniteOrZero(source.P90),
            Ci99 = source.Ci99.Where(double.IsFinite).ToArray(),
            Ci95 = source.Ci95.Where(double.IsFinite).ToArray(),
            Ci90 = source.Ci90.Where(double.IsFinite).ToArray(),
            IsBimodal = source.IsBimodal,
            PeakCount = source.PeakCount,
            Metrics = CopyFiniteMetrics(source.Metrics),
            MetricsData = source.MetricsData.ToDictionary(
                item => item.Key,
                item => item.Value.Where(double.IsFinite).ToList()),
            AdditionalMetrics = CopyFiniteMetrics(source.AdditionalMetrics),
            Status = source.Status,
            OutliersThreshold = FiniteOrZero(source.OutliersThreshold),
            LastStandardOutput = source.LastStandardOutput,
        };
    }

    private static DataPoint CreateExportDataPoint(DataPoint source)
    {
        var dataPoint = new DataPoint
        {
            Start = source.Start,
            End = source.End,
            Duration = source.Duration,
            Metrics = CopyFiniteMetrics(source.Metrics),
            StandardOutput = source.StandardOutput,
            Scenario = source.Scenario,
            AssertResults = source.AssertResults,
        };
        return dataPoint;
    }

    private static Dictionary<string, double> CopyFiniteMetrics(IReadOnlyDictionary<string, double> source)
    {
        return source
            .Where(item => double.IsFinite(item.Value))
            .ToDictionary(item => item.Key, item => item.Value);
    }

    private static double FiniteOrZero(double value) => double.IsFinite(value) ? value : 0;
}

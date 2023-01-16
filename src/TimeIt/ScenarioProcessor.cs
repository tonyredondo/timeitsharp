using System.Diagnostics;
using System.Runtime.InteropServices;
using MathNet.Numerics.Statistics;
using Spectre.Console;
using TimeIt.Common.Configuration;
using TimeIt.Common.Results;

namespace TimeIt;

public class ScenarioProcessor
{
    private readonly Config _configuration;

    public ScenarioProcessor(Config configuration)
    {
        _configuration = configuration;
    }

    public void PrepareScenario(Scenario scenario)
    {
        if (string.IsNullOrEmpty(scenario.ProcessName))
        {
            scenario.ProcessName = _configuration.ProcessName;
        }

        scenario.ProcessName = Utils.ReplaceCustomVars(scenario.ProcessName ?? string.Empty);

        if (string.IsNullOrEmpty(scenario.ProcessArguments))
        {
            scenario.ProcessArguments = _configuration.ProcessArguments;
        }

        scenario.ProcessArguments = Utils.ReplaceCustomVars(scenario.ProcessArguments ?? string.Empty);

        if (string.IsNullOrEmpty(scenario.WorkingDirectory))
        {
            scenario.WorkingDirectory = _configuration.WorkingDirectory;
        }

        scenario.WorkingDirectory = Utils.ReplaceCustomVars(scenario.WorkingDirectory ?? string.Empty);

        foreach (var item in scenario.EnvironmentVariables)
        {
            scenario.EnvironmentVariables[item.Key] = Utils.ReplaceCustomVars(item.Value);
        }

        foreach (var item in _configuration.EnvironmentVariables)
        {
            var value = Utils.ReplaceCustomVars(item.Value);
            if (!scenario.EnvironmentVariables.ContainsKey(item.Key))
            {
                scenario.EnvironmentVariables[item.Key] = value;
            }
        }

        // Add the .NET startup hook to collect metrics
        if (typeof(StartupHook).Assembly.Location is { Length: > 0 } startupHookLocation)
        {
            if (scenario.EnvironmentVariables.TryGetValue(Constants.StartupHookEnvironmentVariable,
                    out var startupHook))
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    scenario.EnvironmentVariables[Constants.StartupHookEnvironmentVariable] =
                        $"{startupHookLocation};{startupHook}";
                }
                else
                {
                    scenario.EnvironmentVariables[Constants.StartupHookEnvironmentVariable] =
                        $"{startupHookLocation}:{startupHook}";
                }
            }
            else
            {
                scenario.EnvironmentVariables[Constants.StartupHookEnvironmentVariable] = startupHookLocation;
            }

            scenario.MetricsJsonFilePath = Path.GetTempFileName();
            scenario.EnvironmentVariables[Constants.TimeItMetricsTemporalPathEnvironmentVariable] =
                scenario.MetricsJsonFilePath;
        }
        else
        {
            AnsiConsole.MarkupLine("[red]Startup hook location is empty.[/]");
        }

        if (scenario.Timeout.MaxDuration <= 0 && _configuration.Timeout.MaxDuration > 0)
        {
            scenario.Timeout.MaxDuration = _configuration.Timeout.MaxDuration;
        }

        if (string.IsNullOrEmpty(scenario.Timeout.ProcessName))
        {
            scenario.Timeout.ProcessName = _configuration.Timeout.ProcessName;
        }

        scenario.Timeout.ProcessName = Utils.ReplaceCustomVars(scenario.Timeout.ProcessName ?? string.Empty);

        if (string.IsNullOrEmpty(scenario.Timeout.ProcessArguments))
        {
            scenario.Timeout.ProcessArguments = _configuration.Timeout.ProcessArguments;
        }

        scenario.Timeout.ProcessArguments = Utils.ReplaceCustomVars(scenario.Timeout.ProcessArguments ?? string.Empty);
    }

    public void CleanScenario(Scenario scenario)
    {
        // Try to clean the metrics temporal file
        if (!string.IsNullOrEmpty(scenario.MetricsJsonFilePath) && File.Exists(scenario.MetricsJsonFilePath))
        {
            try
            {
                File.Delete(scenario.MetricsJsonFilePath);
            }
            catch
            {
                // do nothing
            }
        }
    }

    public async Task<ScenarioResult> ProcessScenarioAsync(Scenario scenario)
    {
        Stopwatch? watch = null;
        AnsiConsole.MarkupLine("[dodgerblue1]Scenario:[/] {0}", scenario.Name);
        AnsiConsole.Markup("  [gold3_1]Warming up[/]");
        watch = Stopwatch.StartNew();
        await RunScenarioAsync(_configuration.WarmUpCount, scenario).ConfigureAwait(false);
        AnsiConsole.MarkupLine("    Duration: {0}s", watch.Elapsed.TotalSeconds);
        AnsiConsole.Markup("  [green3]Run[/]");
        var start = DateTime.UtcNow;
        watch = Stopwatch.StartNew();
        var dataPoints = await RunScenarioAsync(_configuration.Count, scenario).ConfigureAwait(false);
        watch.Stop();
        AnsiConsole.MarkupLine("    Duration: {0}s", watch.Elapsed.TotalSeconds);
        AnsiConsole.WriteLine();

        var durations = new List<double>();
        var metricsData = new Dictionary<string, List<double>>();
        var mapErrors = new HashSet<string>();
        foreach (var item in dataPoints)
        {
            durations.Add(item.Duration.TotalNanoseconds);
            if (!string.IsNullOrEmpty(item.Error))
            {
                mapErrors.Add(item.Error);
            }

            foreach (var kv in item.Metrics)
            {
                if (!metricsData.TryGetValue(kv.Key, out var metricsItem))
                {
                    metricsItem = new List<double>();
                    metricsData[kv.Key] = metricsItem;
                }

                metricsItem.Add(kv.Value);
            }
        }

        var errorString = string.Join(Environment.NewLine, mapErrors);

        // Get outliers
        var newDurations = Utils.RemoveOutliers(durations, 2.0).ToList();
        var outliers = durations.Where(d => !newDurations.Contains(d)).ToList();
        var durationsCount = durations.Count;
        var outliersCount = durationsCount - newDurations.Count;

        var mean = newDurations.Mean();
        var max = newDurations.Maximum();
        var min = newDurations.Minimum();

        for (var i = 0; i < outliersCount; i++)
        {
            newDurations.Add(mean);
        }

        var stdev = newDurations.StandardDeviation();
        var p99 = newDurations.Percentile(99);
        var p95 = newDurations.Percentile(95);
        var p90 = newDurations.Percentile(90);
        var stderr = stdev / Math.Sqrt(durationsCount);

        // Calculate metrics stats
        var metricsStats = new Dictionary<string, double>();
        foreach (var kv in metricsData)
        {
            var mMean = kv.Value.Mean();
            var mMax = kv.Value.Maximum();
            var mMin = kv.Value.Minimum();
            var mStdDev = kv.Value.StandardDeviation();
            var mStdErr = mStdDev / Math.Sqrt(durationsCount);
            var mP99 = kv.Value.Percentile(99);
            var mP95 = kv.Value.Percentile(95);
            var mP90 = kv.Value.Percentile(90);

            metricsStats[kv.Key + ".mean"] = mMean;
            metricsStats[kv.Key + ".max"] = mMax;
            metricsStats[kv.Key + ".min"] = mMin;
            metricsStats[kv.Key + ".std_dev"] = mStdDev;
            metricsStats[kv.Key + ".std_err"] = mStdErr;
            metricsStats[kv.Key + ".p99"] = mP99;
            metricsStats[kv.Key + ".p95"] = mP95;
            metricsStats[kv.Key + ".p90"] = mP90;
        }

        return new ScenarioResult
        {
            Count = _configuration.Count,
            WarmUpCount = _configuration.WarmUpCount,
            Data = dataPoints,
            Durations = newDurations,
            Outliers = outliers,
            Mean = mean,
            Max = max,
            Min = min,
            Stdev = stdev,
            StdErr = stderr,
            P99 = p99,
            P95 = p95,
            P90 = p90,
            Metrics = metricsStats,
            MetricsData = metricsData,
            Start = start,
            End = start + watch.Elapsed,
            Duration = watch.Elapsed,
            Error = errorString,
            Name = scenario.Name,
            ProcessName = scenario.ProcessName,
            ProcessArguments = scenario.ProcessArguments,
            EnvironmentVariables = scenario.EnvironmentVariables,
            WorkingDirectory = scenario.WorkingDirectory,
            Timeout = scenario.Timeout,
            Tags = scenario.Tags,
        };
    }

    private async Task<List<DataPoint>> RunScenarioAsync(int count, Scenario scenario)
    {
        var dataPoints = new List<DataPoint>();
        AnsiConsole.Markup(" ");
        for (var i = 0; i < count; i++)
        {
            var currentRun = await ProcessCmd.RunAsync(scenario).ConfigureAwait(false);
            dataPoints.Add(currentRun);
            if (!currentRun.ShouldContinue)
            {
                break;
            }

            AnsiConsole.Markup(!string.IsNullOrEmpty(currentRun.Error) ? "[red]x[/]" : "[green].[/]");
        }

        AnsiConsole.WriteLine();

        return dataPoints;
    }
}
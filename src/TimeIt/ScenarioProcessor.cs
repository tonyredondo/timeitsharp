using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using CliWrap;
using CliWrap.Buffered;
using MathNet.Numerics.Statistics;
using Spectre.Console;
using TimeIt.Common.Configuration;
using TimeIt.Common.Results;
using TimeIt.RuntimeMetrics;

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

        if (_configuration.EnableMetrics)
        {
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
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Startup hook location is empty.[/]");
            }
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
#if NET7_0_OR_GREATER
            durations.Add(item.Duration.TotalNanoseconds);
#else
            durations.Add(Utils.FromTimeSpanToNanoseconds(item.Duration));
#endif
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
            var currentRun = await RunCommandAsync(scenario).ConfigureAwait(false);
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

    private static async Task<DataPoint> RunCommandAsync(Scenario scenario)
    {
        // Prepare variables
        var cmdString = scenario.ProcessName ?? string.Empty;
        var cmdArguments = scenario.ProcessArguments ?? string.Empty;
        var workingDirectory = scenario.WorkingDirectory ?? string.Empty;
        var cmdTimeout = scenario.Timeout.MaxDuration;
        var timeoutCmdString = scenario.Timeout.ProcessName ?? string.Empty;
        var timeoutCmdArguments = scenario.Timeout.ProcessArguments ?? string.Empty;

        var cmdEnvironmentVariables = new Dictionary<string, string?>();
        foreach (DictionaryEntry osEnv in Environment.GetEnvironmentVariables())
        {
            if (osEnv.Key?.ToString() is { Length: > 0 } keyString)
            {
                cmdEnvironmentVariables[keyString] = osEnv.Value?.ToString();
            }
        }

        foreach (var envVar in scenario.EnvironmentVariables)
        {
            cmdEnvironmentVariables[envVar.Key] = envVar.Value;
        }

        if (cmdEnvironmentVariables.ContainsKey(Constants.StartupHookEnvironmentVariable))
        {
            cmdEnvironmentVariables[Constants.TimeItMetricsTemporalPathEnvironmentVariable] = Path.GetTempFileName();
        }

        // Setup the command
        var cmd = Cli.Wrap(cmdString)
            .WithEnvironmentVariables(cmdEnvironmentVariables)
            .WithWorkingDirectory(workingDirectory)
            .WithValidation(CommandResultValidation.None);
        if (!string.IsNullOrEmpty(cmdArguments))
        {
            cmd = cmd.WithArguments(cmdArguments);
        }

        // Execute the command
        var dataPoint = new DataPoint
        {
            ShouldContinue = true
        };
        if (cmdTimeout <= 0)
        {
            var cmdResult = await cmd.ExecuteBufferedAsync().ConfigureAwait(false);
            dataPoint.End = DateTime.UtcNow;
            dataPoint.Duration = cmdResult.RunTime;
            dataPoint.Start = dataPoint.End - dataPoint.Duration;
            if (cmdResult.ExitCode != 0)
            {
                dataPoint.Error = cmdResult.StandardError + Environment.NewLine + cmdResult.StandardOutput;
            }
        }
        else
        {
            CancellationTokenSource? timeoutCts = null;
            var cmdCts = new CancellationTokenSource();
            dataPoint.Start = DateTime.UtcNow;
            var cmdTask = cmd.ExecuteBufferedAsync(cmdCts.Token);

            if (!string.IsNullOrEmpty(timeoutCmdString))
            {
                timeoutCts = new CancellationTokenSource();
                _ = RunCommandTimeoutAsync(TimeSpan.FromSeconds(cmdTimeout), timeoutCmdString, timeoutCmdArguments,
                    workingDirectory, cmdTask.ProcessId, () => cmdCts.Cancel(), timeoutCts.Token);
            }
            else
            {
                cmdCts.CancelAfter(TimeSpan.FromSeconds(cmdTimeout));
            }

            try
            {
                var cmdResult = await cmdTask.ConfigureAwait(false);
                dataPoint.End = DateTime.UtcNow;
                timeoutCts?.Cancel();
                dataPoint.Duration = cmdResult.RunTime;
                dataPoint.Start = dataPoint.End - dataPoint.Duration;
                if (cmdResult.ExitCode != 0)
                {
                    dataPoint.Error = cmdResult.StandardError + Environment.NewLine + cmdResult.StandardOutput;
                }
            }
            catch (TaskCanceledException)
            {
                dataPoint.End = DateTime.UtcNow;
                dataPoint.Duration = dataPoint.End - dataPoint.Start;
                dataPoint.Error = "Process timeout.";
            }
            catch (OperationCanceledException)
            {
                dataPoint.End = DateTime.UtcNow;
                dataPoint.Duration = dataPoint.End - dataPoint.Start;
                dataPoint.Error = "Process timeout.";
            }
        }

        // Write metrics
        if (cmdEnvironmentVariables.TryGetValue(Constants.TimeItMetricsTemporalPathEnvironmentVariable,
                out var metricsFilePath))
        {
            if (File.Exists(metricsFilePath))
            {
                DateTime? inProcStartDate = null;
                DateTime? inProcEndDate = null;
                var metrics = new Dictionary<string, double>();
                var metricsCount = new Dictionary<string, int>();
                foreach (var metricJsonItem in await File.ReadAllLinesAsync(metricsFilePath).ConfigureAwait(false))
                {
                    if (JsonSerializer.Deserialize<FileStatsd.FileStatsdPayload>(metricJsonItem, new JsonSerializerOptions(JsonSerializerDefaults.Web)) is { } metricItem)
                    {
                        if (metricItem.Name is not null)
                        {
                            if (metricItem.Name == Constants.ProcessStartTimeUtcMetricName)
                            {
                                inProcStartDate = DateTime.FromBinary((long)metricItem.Value);
                                metrics[Constants.ProcessTimeToStartMetricName] = (inProcStartDate.Value - dataPoint.Start).TotalMilliseconds;
                                if (inProcEndDate != null)
                                {
                                    metrics[Constants.ProcessInternalDurationMetricName] = (inProcEndDate.Value - inProcStartDate.Value).TotalMilliseconds;
                                }

                                continue;
                            }

                            if (metricItem.Name == Constants.ProcessEndTimeUtcMetricName)
                            {
                                inProcEndDate = DateTime.FromBinary((long)metricItem.Value);
                                metrics[Constants.ProcessTimeToEndMetricName] = (dataPoint.End - inProcEndDate.Value).TotalMilliseconds;
                                if (inProcStartDate != null)
                                {
                                    metrics[Constants.ProcessInternalDurationMetricName] = (inProcEndDate.Value - inProcStartDate.Value).TotalMilliseconds;
                                }
                                
                                continue;
                            }

                            if (metricItem.Type == "counter")
                            {
                                metrics[metricItem.Name] = metricItem.Value;
                            }
                            else if (metricItem.Type is "gauge" or "timer")
                            {
                                if (metrics.TryGetValue(metricItem.Name, out var oldValue))
                                {
                                    metrics[metricItem.Name] = oldValue + metricItem.Value;
                                    metricsCount[metricItem.Name]++;
                                }
                                else
                                {
                                    metrics[metricItem.Name] = metricItem.Value;
                                    metricsCount[metricItem.Name] = 1;
                                }
                            }
                            else if (metricItem.Type == "increment")
                            {
                                if (metrics.TryGetValue(metricItem.Name, out var oldValue))
                                {
                                    metrics[metricItem.Name] = oldValue + metricItem.Value;
                                }
                                else
                                {
                                    metrics[metricItem.Name] = metricItem.Value;
                                }
                            }
                        }
                    }
                }

                foreach (var mItem in metricsCount)
                {
                    metrics[mItem.Key] /= mItem.Value;
                }

                dataPoint.Metrics = metrics;

                try
                {
                    File.Delete(metricsFilePath);
                }
                catch
                {
                    // Do nothing
                }
            }
        }

        return dataPoint;
    }

    private static async Task RunCommandTimeoutAsync(TimeSpan timeout, string timeoutCmd, string timeoutArgument,
        string workingDirectory, int targetPid, Action? targetCancellation, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(timeout, cancellationToken).ConfigureAwait(false);
            if (!cancellationToken.IsCancellationRequested)
            {
                timeoutCmd = timeoutCmd.Replace("%pid%", targetPid.ToString());
                timeoutArgument = timeoutArgument.Replace("%pid%", targetPid.ToString());
                var cmd = Cli.Wrap(timeoutCmd)
                    .WithWorkingDirectory(workingDirectory)
                    .WithValidation(CommandResultValidation.None);
                if (!string.IsNullOrEmpty(timeoutArgument))
                {
                    cmd = cmd.WithArguments(timeoutArgument);
                }

                var cmdResult = await cmd.ExecuteBufferedAsync().ConfigureAwait(false);
                if (cmdResult.ExitCode != 0)
                {
                    AnsiConsole.MarkupLine($"[red]{cmdResult.StandardError}[/]");
                    AnsiConsole.MarkupLine(cmdResult.StandardOutput);
                }
            }
        }
        catch (TaskCanceledException)
        {
            // Do nothing
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]{ex.Message}[/]");
        }
        finally
        {
            targetCancellation?.Invoke();
        }
    }
}
using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using CliWrap;
using CliWrap.Buffered;
using MathNet.Numerics.Statistics;
using Spectre.Console;
using TimeIt.Common.Assertors;
using TimeIt.Common.Configuration;
using TimeIt.Common.Results;
using Status = TimeIt.Common.Results.Status;

namespace TimeIt;

public class ScenarioProcessor
{
    private readonly Config _configuration;
    private readonly TemplateVariables _templateVariables;
    private readonly IReadOnlyList<IAssertor> _assertors;

    public ScenarioProcessor(Config configuration, TemplateVariables templateVariables, IReadOnlyList<IAssertor> assertors)
    {
        _configuration = configuration;
        _templateVariables = templateVariables;
        _assertors = assertors;
    }

    public void PrepareScenario(Scenario scenario)
    {
        if (string.IsNullOrEmpty(scenario.ProcessName))
        {
            scenario.ProcessName = _configuration.ProcessName;
        }

        scenario.ProcessName = _templateVariables.Expand(scenario.ProcessName ?? string.Empty);

        if (string.IsNullOrEmpty(scenario.ProcessArguments))
        {
            scenario.ProcessArguments = _configuration.ProcessArguments;
        }

        scenario.ProcessArguments = _templateVariables.Expand(scenario.ProcessArguments ?? string.Empty);

        if (string.IsNullOrEmpty(scenario.WorkingDirectory))
        {
            scenario.WorkingDirectory = _configuration.WorkingDirectory;
        }

        scenario.WorkingDirectory = _templateVariables.Expand(scenario.WorkingDirectory ?? string.Empty);

        foreach (var item in scenario.EnvironmentVariables)
        {
            scenario.EnvironmentVariables[item.Key] = _templateVariables.Expand(item.Value);
        }

        foreach (var item in _configuration.EnvironmentVariables)
        {
            var value = _templateVariables.Expand(item.Value);
            scenario.EnvironmentVariables.TryAdd(item.Key, value);
        }

        foreach (var (tagName, tagValue) in _configuration.Tags)
        {
            var value = _templateVariables.Expand(tagValue);
            scenario.Tags.TryAdd(tagName, value);
        }

        for (var i = 0; i < scenario.PathValidations.Count; i++)
        {
            scenario.PathValidations[i] = _templateVariables.Expand(scenario.PathValidations[i]);
        }

        foreach (var item in _configuration.PathValidations)
        {
            var value = _templateVariables.Expand(item);
            var idx = scenario.PathValidations.IndexOf(value);
            if (idx == -1)
            {
                scenario.PathValidations.Add(value);
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

        scenario.Timeout.ProcessName = _templateVariables.Expand(scenario.Timeout.ProcessName ?? string.Empty);

        if (string.IsNullOrEmpty(scenario.Timeout.ProcessArguments))
        {
            scenario.Timeout.ProcessArguments = _configuration.Timeout.ProcessArguments;
        }

        scenario.Timeout.ProcessArguments = _templateVariables.Expand(scenario.Timeout.ProcessArguments ?? string.Empty);
    }

    public void CleanScenario(Scenario scenario)
    {
    }

    public async Task<ScenarioResult> ProcessScenarioAsync(int index, Scenario scenario)
    {
        Stopwatch? watch = null;
        AnsiConsole.MarkupLine("[dodgerblue1]Scenario:[/] {0}", scenario.Name);

        if (scenario.PathValidations.Count > 0)
        {
            AnsiConsole.MarkupLine("  [gold3_1]Path validations.[/]");
            string? validationErrors = null;
            foreach (var path in scenario.PathValidations)
            {
                if (!File.Exists(path))
                {
                    validationErrors += $"File '{path}' from path validations not found.{Environment.NewLine}";
                }
            }

            if (!string.IsNullOrEmpty(validationErrors))
            {
                return new ScenarioResult
                {
                    Count = _configuration.Count,
                    WarmUpCount = _configuration.WarmUpCount,
                    Data = new List<DataPoint>(),
                    Durations = new List<double>(),
                    Outliers = new List<double>(),
                    Metrics = new Dictionary<string, double>(),
                    MetricsData = new Dictionary<string, List<double>>(),
                    Error = validationErrors,
                    Name = scenario.Name,
                    ProcessName = scenario.ProcessName,
                    ProcessArguments = scenario.ProcessArguments,
                    EnvironmentVariables = scenario.EnvironmentVariables,
                    PathValidations = scenario.PathValidations,
                    WorkingDirectory = scenario.WorkingDirectory,
                    Timeout = scenario.Timeout,
                    Tags = scenario.Tags,
                    Status = Status.Failed,
                };
            }
        }

        AnsiConsole.Markup("  [gold3_1]Warming up[/]");
        watch = Stopwatch.StartNew();
        await RunScenarioAsync(_configuration.WarmUpCount, index, scenario, false).ConfigureAwait(false);
        AnsiConsole.MarkupLine("    Duration: {0}s", watch.Elapsed.TotalSeconds);
        AnsiConsole.Markup("  [green3]Run[/]");
        var start = DateTime.UtcNow;
        watch = Stopwatch.StartNew();
        var dataPoints = await RunScenarioAsync(_configuration.Count, index, scenario, true).ConfigureAwait(false);
        watch.Stop();
        AnsiConsole.MarkupLine("    Duration: {0}s", watch.Elapsed.TotalSeconds);
        AnsiConsole.WriteLine();

        var durations = new List<double>();
        var metricsData = new Dictionary<string, List<double>>();
        foreach (var item in dataPoints)
        {
            if (item.Status != Status.Passed)
            {
                continue;
            }

#if NET7_0_OR_GREATER
            durations.Add(item.Duration.TotalNanoseconds);
#else
            durations.Add(Utils.FromTimeSpanToNanoseconds(item.Duration));
#endif
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

        // Get outliers
        var newDurations = Utils.RemoveOutliers(durations, 2.0).ToList();
        var outliers = durations.Where(d => !newDurations.Contains(d)).ToList();
        var mean = newDurations.Mean();
        var max = newDurations.Maximum();
        var min = newDurations.Minimum();
        var stdev = newDurations.StandardDeviation();
        var p99 = newDurations.Percentile(99);
        var p95 = newDurations.Percentile(95);
        var p90 = newDurations.Percentile(90);
        var stderr = stdev / Math.Sqrt(newDurations.Count);

        // Calculate metrics stats
        var metricsStats = new Dictionary<string, double>();
        foreach (var key in metricsData.Keys)
        {
            var originalMetricsValue = metricsData[key];
            var metricsValue = Utils.RemoveOutliers(originalMetricsValue, 3.0).ToList();
            metricsData[key] = metricsValue;
            var mMean = metricsValue.Mean();
            var mMax = metricsValue.Maximum();
            var mMin = metricsValue.Minimum();
            var mStdDev = metricsValue.StandardDeviation();
            var mStdErr = mStdDev / Math.Sqrt(metricsValue.Count);
            var mP99 = metricsValue.Percentile(99);
            var mP95 = metricsValue.Percentile(95);
            var mP90 = metricsValue.Percentile(90);

            metricsStats[key + ".n"] = metricsValue.Count;
            metricsStats[key + ".mean"] = mMean;
            metricsStats[key + ".max"] = mMax;
            metricsStats[key + ".min"] = mMin;
            metricsStats[key + ".std_dev"] = mStdDev;
            metricsStats[key + ".std_err"] = mStdErr;
            metricsStats[key + ".p99"] = mP99;
            metricsStats[key + ".p95"] = mP95;
            metricsStats[key + ".p90"] = mP90;
            metricsStats[key + ".outliers"] = originalMetricsValue.Count - metricsValue.Count;
        }

        var assertResponse = ScenarioAssertion(dataPoints);
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
            Error = assertResponse.Message,
            Name = scenario.Name,
            ProcessName = scenario.ProcessName,
            ProcessArguments = scenario.ProcessArguments,
            EnvironmentVariables = scenario.EnvironmentVariables,
            PathValidations = scenario.PathValidations,
            WorkingDirectory = scenario.WorkingDirectory,
            Timeout = scenario.Timeout,
            Tags = scenario.Tags,
            Status = assertResponse.Status,
        };
    }

    private async Task<List<DataPoint>> RunScenarioAsync(int count, int index, Scenario scenario, bool checkShouldContinue)
    {
        var dataPoints = new List<DataPoint>();
        AnsiConsole.Markup(" ");
        for (var i = 0; i < count; i++)
        {
            var currentRun = await RunCommandAsync(index, scenario).ConfigureAwait(false);
            dataPoints.Add(currentRun);
            AnsiConsole.Markup(currentRun.Status == Status.Failed ? "[red]x[/]" : "[green].[/]");

            if (checkShouldContinue && !currentRun.ShouldContinue)
            {
                break;
            }
        }

        AnsiConsole.WriteLine();

        return dataPoints;
    }

    private async Task<DataPoint> RunCommandAsync(int index, Scenario scenario)
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

        // add working directory as a path to resolve binary
        var pathWithWorkingDir = workingDirectory + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH");
        Environment.SetEnvironmentVariable("PATH", pathWithWorkingDir);

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
            ExecuteAssertions(index, scenario.Name, dataPoint, cmdResult);
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
                ExecuteAssertions(index, scenario.Name, dataPoint, cmdResult);
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
            metricsFilePath ??= string.Empty;
            if (File.Exists(metricsFilePath))
            {
                DateTime? inProcStartDate = null;
                DateTime? inProcMainStartDate = null;
                DateTime? inProcMainEndDate = null;
                DateTime? inProcEndDate = null;
                var metrics = new Dictionary<string, double>();
                var metricsCount = new Dictionary<string, int>();
                foreach (var metricJsonItem in await File.ReadAllLinesAsync(metricsFilePath).ConfigureAwait(false))
                {
                    if (JsonSerializer.Deserialize<FileStatsdPayload>(metricJsonItem, new JsonSerializerOptions(JsonSerializerDefaults.Web)) is { } metricItem)
                    {
                        if (metricItem.Name is not null)
                        {
                            static void EnsureMainDuration(Dictionary<string, double> values, DateTime? mainStartDate, DateTime? mainEndDate)
                            {
                                if (mainStartDate is not null && mainEndDate is not null)
                                {
                                    values[Constants.ProcessInternalDurationMetricName] =
                                        (mainEndDate.Value - mainStartDate.Value).TotalMilliseconds;
                                }
                            }

                            static void EnsureStartupHookOverhead(
                                DataPoint point,
                                Dictionary<string, double> values,
                                DateTime? startDate,
                                DateTime? mainStartDate,
                                DateTime? mainEndDate,
                                DateTime? endDate)
                            {
                                if (startDate is not null &&
                                    mainStartDate is not null &&
                                    mainEndDate is not null &&
                                    endDate is not null)
                                {
                                    var mainDuration = (mainEndDate.Value - mainStartDate.Value).TotalMilliseconds;
                                    var internalDuration = (endDate.Value - startDate.Value).TotalMilliseconds;
                                    var overheadDuration = internalDuration - mainDuration;
                                    var globalDuration = (point.End - point.Start).TotalMilliseconds; 
                                    values[Constants.ProcessStartupHookOverheadMetricName] = overheadDuration;
                                    values[Constants.ProcessCorrectedDurationMetricName] = globalDuration - overheadDuration;
                                }
                            }
                            
                            if (metricItem.Name == Constants.ProcessStartTimeUtcMetricName)
                            {
                                inProcStartDate = DateTime.FromBinary((long)metricItem.Value);
                                metrics[Constants.ProcessTimeToStartMetricName] = (inProcStartDate.Value - dataPoint.Start).TotalMilliseconds;
                                EnsureStartupHookOverhead(dataPoint, metrics, inProcStartDate, inProcMainStartDate,
                                    inProcMainEndDate, inProcEndDate);
                                continue;
                            }

                            if (metricItem.Name == Constants.MainMethodStartTimeUtcMetricName)
                            {
                                inProcMainStartDate = DateTime.FromBinary((long)metricItem.Value);
                                metrics[Constants.ProcessTimeToMainMetricName] = (inProcMainStartDate.Value - dataPoint.Start).TotalMilliseconds;
                                EnsureMainDuration(metrics, inProcMainStartDate, inProcMainEndDate);
                                EnsureStartupHookOverhead(dataPoint, metrics, inProcStartDate, inProcMainStartDate,
                                    inProcMainEndDate, inProcEndDate);
                                continue;
                            }

                            if (metricItem.Name == Constants.MainMethodEndTimeUtcMetricName)
                            {
                                inProcMainEndDate = DateTime.FromBinary((long)metricItem.Value);
                                metrics[Constants.ProcessTimeToMainEndMetricName] = (dataPoint.End - inProcMainEndDate.Value).TotalMilliseconds;
                                EnsureMainDuration(metrics, inProcMainStartDate, inProcMainEndDate);
                                EnsureStartupHookOverhead(dataPoint, metrics, inProcStartDate, inProcMainStartDate,
                                    inProcMainEndDate, inProcEndDate);
                                continue;
                            }
                            
                            if (metricItem.Name == Constants.ProcessEndTimeUtcMetricName)
                            {
                                inProcEndDate = DateTime.FromBinary((long)metricItem.Value);
                                metrics[Constants.ProcessTimeToEndMetricName] = (dataPoint.End - inProcEndDate.Value).TotalMilliseconds;
                                EnsureStartupHookOverhead(dataPoint, metrics, inProcStartDate, inProcMainStartDate,
                                    inProcMainEndDate, inProcEndDate);
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

    private void ExecuteAssertions(int scenarioId, string scenarioName, DataPoint dataPoint, BufferedCommandResult cmdResult)
    {
        var assertionData = new AssertionData(scenarioId, scenarioName, dataPoint.Start, dataPoint.End,
            dataPoint.Duration, cmdResult.ExitCode, cmdResult.StandardOutput, cmdResult.StandardError);
        var assertionResult = ExecutionAssertion(in assertionData);
        dataPoint.AssertResults = assertionResult;
        if (assertionResult.Status == Status.Failed && string.IsNullOrEmpty(dataPoint.Error))
        {
            dataPoint.Error = "Execution has failed by the status value = Failed.";
        }
    }

    private async Task RunCommandTimeoutAsync(TimeSpan timeout, string timeoutCmd, string timeoutArgument,
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

    private AssertResponse ScenarioAssertion(IReadOnlyList<DataPoint> dataPoints)
    {
        if (_assertors is null || _assertors.Count == 0)
        {
            return new AssertResponse(Status.Passed);
        }

        Status status = Status.Passed;
        bool shouldContinue = true;
        HashSet<string>? messagesHashSet = null;
        foreach (var assertor in _assertors)
        {
            if (assertor is null)
            {
                continue;
            }

            var result = assertor.ScenarioAssertion(dataPoints);
            shouldContinue = shouldContinue && result.ShouldContinue;
            if (result.Status == Status.Failed)
            {
                status = Status.Failed;
            }

            if (!string.IsNullOrEmpty(result.Message))
            {
                messagesHashSet ??= new HashSet<string>();
                messagesHashSet.Add(result.Message);
            }
        }

        string message = string.Empty;
        if (messagesHashSet?.Count > 0)
        {
            message = string.Join(Environment.NewLine, messagesHashSet);
        }

        return new AssertResponse(status, shouldContinue, message);
    }

    private AssertResponse ExecutionAssertion(in AssertionData data)
    {
        if (_assertors is null || _assertors.Count == 0)
        {
            return new AssertResponse(Status.Passed);
        }

        Status status = Status.Passed;
        bool shouldContinue = true;
        HashSet<string>? messagesHashSet = null;
        foreach (var assertor in _assertors)
        {
            if (assertor is null)
            {
                continue;
            }

            var result = assertor.ExecutionAssertion(in data);
            shouldContinue = shouldContinue && result.ShouldContinue;
            if (result.Status == Status.Failed)
            {
                status = Status.Failed;
            }

            if (!string.IsNullOrEmpty(result.Message))
            {
                messagesHashSet ??= new HashSet<string>();
                messagesHashSet.Add(result.Message);
            }
        }

        string message = string.Empty;
        if (messagesHashSet?.Count > 0)
        {
            message = string.Join(Environment.NewLine, messagesHashSet);
        }

        return new AssertResponse(status, shouldContinue, message);
    }
}
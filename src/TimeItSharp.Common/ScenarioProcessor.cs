using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text.Json;
using CliWrap;
using CliWrap.Buffered;
using MathNet.Numerics.Statistics;
using Spectre.Console;
using TimeItSharp.Common.Assertors;
using TimeItSharp.Common.Configuration;
using TimeItSharp.Common.Results;
using TimeItSharp.Common.Services;
using Status = TimeItSharp.Common.Results.Status;

namespace TimeItSharp.Common;

internal sealed class ScenarioProcessor
{
    private readonly Config _configuration;
    private readonly TemplateVariables _templateVariables;
    private readonly IReadOnlyList<IAssertor> _assertors;
    private readonly IReadOnlyList<IService> _services;
    private readonly TimeItCallbacks.CallbacksTriggers _callbacksTriggers;

    public ScenarioProcessor(
        Config configuration,
        TemplateVariables templateVariables,
        IReadOnlyList<IAssertor> assertors,
        IReadOnlyList<IService> services,
        TimeItCallbacks.CallbacksTriggers callbacksTriggers)
    {
        _configuration = configuration;
        _templateVariables = templateVariables;
        _assertors = assertors;
        _services = services;
        _callbacksTriggers = callbacksTriggers;
    }

    [UnconditionalSuppressMessage("SingleFile", "IL3000:Avoid accessing Assembly file path when publishing as a single file", Justification = "Case is being handled")]
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
            var key = _templateVariables.Expand(tagName);
            if (tagValue is string strTagValue)
            {
                scenario.Tags.TryAdd(key, strTagValue);
            }
            else
            {
                scenario.Tags.TryAdd(key, tagValue);
            }
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
            var startupHookAssemblyLocation = typeof(StartupHook).Assembly.Location;

            // Add the .NET startup hook to collect metrics
            if (startupHookAssemblyLocation is { Length: > 0 } startupHookLocation)
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

                if (!string.IsNullOrEmpty(_configuration.MetricsProcessName))
                {
                    scenario.EnvironmentVariables[Constants.TimeItMetricsProcessName] =
                        _configuration.MetricsProcessName;
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

    public async Task<ScenarioResult?> ProcessScenarioAsync(int index, Scenario scenario, CancellationToken cancellationToken)
    {
        _callbacksTriggers.ScenarioStart(scenario);
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

        watch = Stopwatch.StartNew();
        if (_configuration.WarmUpCount > 0)
        {
            AnsiConsole.Markup("  [gold3_1]Warming up[/]");
            watch.Restart();
            await RunScenarioAsync(_configuration.WarmUpCount, index, scenario, TimeItPhase.WarmUp, false,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            watch.Stop();
            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            AnsiConsole.MarkupLine("    Duration: {0}s", Math.Round(watch.Elapsed.TotalSeconds, 3));
        }

        AnsiConsole.Markup("  [green3]Run[/]");
        var start = DateTime.UtcNow;
        watch.Restart();
        var dataPoints = await RunScenarioAsync(_configuration.Count, index, scenario, TimeItPhase.Run, true, cancellationToken: cancellationToken).ConfigureAwait(false);
        watch.Stop();
        if (cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        watch.Stop();
        AnsiConsole.MarkupLine("    Duration: {0}s", Math.Round(watch.Elapsed.TotalSeconds, 3));

        if (_configuration.CoolDownCount > 0)
        {
            AnsiConsole.Markup("  [gold3_1]Cooling down[/]");
            watch.Restart();
            await RunScenarioAsync(_configuration.CoolDownCount, index, scenario, TimeItPhase.CoolDown, false,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            watch.Stop();
            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            AnsiConsole.MarkupLine("    Duration: {0}s", Math.Round(watch.Elapsed.TotalSeconds, 3));
        }

        AnsiConsole.WriteLine();

        var lastStandardOutput = string.Empty;
        var durations = new List<double>();
        var metricsData = new Dictionary<string, List<double>>();
        foreach (var item in dataPoints)
        {
            if (!string.IsNullOrEmpty(item.StandardOutput))
            {
                lastStandardOutput = item.StandardOutput;
            }

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
        var newDurations = new List<double>();
        var outliers = new List<double>();
        var threshold = 0.4d;
        var peakCount = 0;
        var histogram = Array.Empty<int>();
        var labels = Array.Empty<Range<double>>();
        var isBimodal = false;
        while (threshold < 2.0d)
        {
            newDurations = Utils.RemoveOutliers(durations, threshold).ToList();
            outliers = durations.Where(d => !newDurations.Contains(d)).ToList();
            isBimodal = Utils.IsBimodal(CollectionsMarshal.AsSpan(newDurations), out peakCount, out histogram, out labels, Math.Min(10, Math.Max(_configuration.Count / 10, 3)));
            var outliersPercent = ((double)outliers.Count / durations.Count) * 100;
            if (outliersPercent < 20 && !isBimodal)
            {
                // outliers must be not more than 20% of the data
                break;
            }

            threshold += 0.1;
        }

        var mean = newDurations.Mean();
        var median = newDurations.Median();
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
            var metricsValue = new List<double>();
            var metricsOutliers = new List<double>();
            var metricsThreshold = 0.4d;
            while (metricsThreshold < 3.0d)
            {
                metricsValue = Utils.RemoveOutliers(originalMetricsValue, metricsThreshold).ToList();
                metricsOutliers = originalMetricsValue.Where(d => !metricsValue.Contains(d)).ToList();
                var outliersPercent = ((double)metricsOutliers.Count / originalMetricsValue.Count) * 100;
                if (outliersPercent < 20)
                {
                    // outliers must be not more than 20% of the data
                    break;
                }

                metricsThreshold += 0.1;
            }
            
            metricsData[key] = metricsValue;
            var mMean = metricsValue.Mean();
            var mMedian = metricsValue.Median();
            var mMax = metricsValue.Maximum();
            var mMin = metricsValue.Minimum();
            var mStdDev = metricsValue.StandardDeviation();
            var mStdErr = mStdDev / Math.Sqrt(metricsValue.Count);
            var mP99 = metricsValue.Percentile(99);
            var mP95 = metricsValue.Percentile(95);
            var mP90 = metricsValue.Percentile(90);

            metricsStats[key + ".n"] = metricsValue.Count;
            metricsStats[key + ".mean"] = mMean;
            metricsStats[key + ".median"] = mMedian;
            metricsStats[key + ".max"] = mMax;
            metricsStats[key + ".min"] = mMin;
            metricsStats[key + ".std_dev"] = mStdDev;
            metricsStats[key + ".std_err"] = mStdErr;
            metricsStats[key + ".p99"] = mP99;
            metricsStats[key + ".p95"] = mP95;
            metricsStats[key + ".p90"] = mP90;
            metricsStats[key + ".outliers"] = metricsOutliers.Count;
            metricsStats[key + ".outliers_threshold"] = metricsThreshold;
        }

        var assertResponse = ScenarioAssertion(dataPoints);
        var scenarioResult = new ScenarioResult
        {
            Scenario = scenario,
            Count = _configuration.Count,
            WarmUpCount = _configuration.WarmUpCount,
            Data = dataPoints,
            Durations = newDurations,
            Outliers = outliers,
            Mean = mean,
            Median = median,
            Max = max,
            Min = min,
            Stdev = stdev,
            StdErr = stderr,
            P99 = p99,
            P95 = p95,
            P90 = p90,
            IsBimodal = isBimodal,
            PeakCount = peakCount,
            Histogram = histogram,
            HistogramLabels = labels,
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
            OutliersThreshold = threshold,
            LastStandardOutput = lastStandardOutput,
        };

        _callbacksTriggers.ScenarioFinish(scenarioResult);
        return scenarioResult;
    }

    private async Task<List<DataPoint>> RunScenarioAsync(int count, int index, Scenario scenario, TimeItPhase phase, bool checkShouldContinue, CancellationToken cancellationToken)
    {
        var dataPoints = new List<DataPoint>();
        AnsiConsole.Markup(" ");
        for (var i = 0; i < count; i++)
        {
            var currentRun = await RunCommandAsync(index, scenario, phase, cancellationToken).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
            {
                AnsiConsole.Markup("[red]cancelled[/]");
                break;
            }

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

    private async Task<DataPoint> RunCommandAsync(int index, Scenario scenario, TimeItPhase phase, CancellationToken cancellationToken)
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
            ShouldContinue = true,
            Scenario = scenario,
        };
        
        _callbacksTriggers.ExecutionStart(dataPoint, phase, ref cmd);
        if (cmdTimeout <= 0)
        {
            try
            {
                var cmdResult = await cmd.ExecuteBufferedAsync(cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                dataPoint.End = DateTime.UtcNow;
                dataPoint.Duration = cmdResult.RunTime;
                dataPoint.Start = dataPoint.End - dataPoint.Duration;
                dataPoint.StandardOutput = cmdResult.StandardOutput;
                ExecuteAssertions(index, scenario.Name, phase, dataPoint, cmdResult);
            }
            catch (TaskCanceledException)
            {
                dataPoint.End = DateTime.UtcNow;
                dataPoint.Duration = dataPoint.End - dataPoint.Start;
                dataPoint.Error = "Execution cancelled.";
                return dataPoint;
            }
            catch (OperationCanceledException)
            {
                dataPoint.End = DateTime.UtcNow;
                dataPoint.Duration = dataPoint.End - dataPoint.Start;
                dataPoint.Error = "Execution cancelled.";
                return dataPoint;
            }
        }
        else
        {
            CancellationTokenSource? timeoutCts = null;
            var cmdCts = new CancellationTokenSource();
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cmdCts.Token);
            dataPoint.Start = DateTime.UtcNow;
            var cmdTask = cmd.ExecuteBufferedAsync(linkedCts.Token);

            if (!string.IsNullOrEmpty(timeoutCmdString))
            {
                timeoutCts = new CancellationTokenSource();
                _ = RunCommandTimeoutAsync(TimeSpan.FromSeconds(cmdTimeout), timeoutCmdString, timeoutCmdArguments,
                    workingDirectory, cmdTask.ProcessId, () => cmdCts.Cancel(), timeoutCts.Token, cancellationToken);
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
                dataPoint.StandardOutput = cmdResult.StandardOutput;
                ExecuteAssertions(index, scenario.Name, phase, dataPoint, cmdResult);
            }
            catch (TaskCanceledException)
            {
                dataPoint.End = DateTime.UtcNow;
                dataPoint.Duration = dataPoint.End - dataPoint.Start;
                if (cancellationToken.IsCancellationRequested)
                {
                    dataPoint.Error = "Execution cancelled.";
                    return dataPoint;
                }

                dataPoint.Error = "Process timeout.";
            }
            catch (OperationCanceledException)
            {
                dataPoint.End = DateTime.UtcNow;
                dataPoint.Duration = dataPoint.End - dataPoint.Start;
                if (cancellationToken.IsCancellationRequested)
                {
                    dataPoint.Error = "Execution cancelled.";
                    return dataPoint;
                }

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
                    try
                    {
                        if (JsonSerializer.Deserialize<FileStatsdPayload>(metricJsonItem,
                                FileStatsdPayloadContext.Default.FileStatsdPayload) is { } metricItem)
                        {
                            if (metricItem.Name is not null)
                            {
                                static void EnsureMainDuration(Dictionary<string, double> values,
                                    DateTime? mainStartDate, DateTime? mainEndDate)
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
                                        values[Constants.ProcessCorrectedDurationMetricName] =
                                            globalDuration - overheadDuration;
                                    }
                                }

                                if (metricItem.Name == Constants.ProcessStartTimeUtcMetricName)
                                {
                                    inProcStartDate = DateTime.FromBinary((long)metricItem.Value);
                                    metrics[Constants.ProcessTimeToStartMetricName] =
                                        (inProcStartDate.Value - dataPoint.Start).TotalMilliseconds;
                                    EnsureStartupHookOverhead(dataPoint, metrics, inProcStartDate, inProcMainStartDate,
                                        inProcMainEndDate, inProcEndDate);
                                    continue;
                                }

                                if (metricItem.Name == Constants.MainMethodStartTimeUtcMetricName)
                                {
                                    inProcMainStartDate = DateTime.FromBinary((long)metricItem.Value);
                                    metrics[Constants.ProcessTimeToMainMetricName] =
                                        (inProcMainStartDate.Value - dataPoint.Start).TotalMilliseconds;
                                    EnsureMainDuration(metrics, inProcMainStartDate, inProcMainEndDate);
                                    EnsureStartupHookOverhead(dataPoint, metrics, inProcStartDate, inProcMainStartDate,
                                        inProcMainEndDate, inProcEndDate);
                                    continue;
                                }

                                if (metricItem.Name == Constants.MainMethodEndTimeUtcMetricName)
                                {
                                    inProcMainEndDate = DateTime.FromBinary((long)metricItem.Value);
                                    metrics[Constants.ProcessTimeToMainEndMetricName] =
                                        (dataPoint.End - inProcMainEndDate.Value).TotalMilliseconds;
                                    EnsureMainDuration(metrics, inProcMainStartDate, inProcMainEndDate);
                                    EnsureStartupHookOverhead(dataPoint, metrics, inProcStartDate, inProcMainStartDate,
                                        inProcMainEndDate, inProcEndDate);
                                    continue;
                                }

                                if (metricItem.Name == Constants.ProcessEndTimeUtcMetricName)
                                {
                                    inProcEndDate = DateTime.FromBinary((long)metricItem.Value);
                                    metrics[Constants.ProcessTimeToEndMetricName] =
                                        (dataPoint.End - inProcEndDate.Value).TotalMilliseconds;
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
                    catch
                    {
                        // Error reading metric item, we just skip that item
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

        _callbacksTriggers.ExecutionEnd(dataPoint, phase);
        return dataPoint;
    }

    private void ExecuteAssertions(int scenarioId, string scenarioName, TimeItPhase phase, DataPoint dataPoint, BufferedCommandResult cmdResult)
    {
        var assertionData = new AssertionData(scenarioId, scenarioName, phase, dataPoint.Start, dataPoint.End,
            dataPoint.Duration, cmdResult.ExitCode, cmdResult.StandardOutput, cmdResult.StandardError, _services);
        var assertionResult = ExecutionAssertion(in assertionData);
        dataPoint.AssertResults = assertionResult;
        if (assertionResult.Status == Status.Failed && string.IsNullOrEmpty(dataPoint.Error))
        {
            dataPoint.Error = "Execution has failed by the status value = Failed.";
        }
    }

    private async Task RunCommandTimeoutAsync(TimeSpan timeout, string timeoutCmd, string timeoutArgument,
        string workingDirectory, int targetPid, Action? targetCancellation, CancellationToken timeoutCancellationToken, CancellationToken applicationCancellationToken)
    {
        try
        {
            using var linkedCts =
                CancellationTokenSource.CreateLinkedTokenSource(timeoutCancellationToken, applicationCancellationToken);
            await Task.Delay(timeout, linkedCts.Token).ConfigureAwait(false);
            if (linkedCts.Token.IsCancellationRequested)
            {
                return;
            }

            var targetPidString = targetPid.ToString();
            var templateVariables = _templateVariables.Clone();
            templateVariables.Add("PID", targetPidString);

            timeoutCmd = templateVariables.Expand(timeoutCmd);
            timeoutCmd = timeoutCmd.Replace("%pid%", targetPidString);

            timeoutArgument = templateVariables.Expand(timeoutArgument);
            timeoutArgument = timeoutArgument.Replace("%pid%", targetPidString);

            var cmd = Cli.Wrap(timeoutCmd)
                .WithWorkingDirectory(workingDirectory)
                .WithValidation(CommandResultValidation.None);
            if (!string.IsNullOrEmpty(timeoutArgument))
            {
                cmd = cmd.WithArguments(timeoutArgument);
            }

            var cmdResult = await cmd.ExecuteBufferedAsync(applicationCancellationToken).ConfigureAwait(false);
            if (cmdResult.ExitCode != 0)
            {
                AnsiConsole.MarkupLine($"[red]{cmdResult.StandardError}[/]");
                AnsiConsole.MarkupLine(cmdResult.StandardOutput);
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
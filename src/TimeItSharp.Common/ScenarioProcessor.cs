using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using CliWrap;
using CliWrap.Buffered;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.Statistics;
using Spectre.Console;
using TimeItSharp.Common.Assertors;
using TimeItSharp.Common.Configuration;
using TimeItSharp.Common.Results;
using TimeItSharp.Common.Services;
using TimeItSharp.RuntimeMetrics;
using Status = TimeItSharp.Common.Results.Status;

namespace TimeItSharp.Common;

internal sealed class ScenarioProcessor
{
    private readonly Config _configuration;
    private readonly TemplateVariables _templateVariables;
    private readonly IReadOnlyList<IAssertor> _assertors;
    private readonly IReadOnlyList<IService> _services;
    private readonly TimeItCallbacks.CallbacksTriggers _callbacksTriggers;

    private static readonly IDictionary EnvironmentVariables = Environment.GetEnvironmentVariables();

    private double _remainingTimeInMinutes;
    
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
        _remainingTimeInMinutes = configuration.MaximumDurationInMinutes;
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
            scenario.EnvironmentVariables[_templateVariables.Expand(item.Key)] = _templateVariables.Expand(item.Value);
        }

        foreach (var item in _configuration.EnvironmentVariables)
        {
            var key = _templateVariables.Expand(item.Key);
            var value = _templateVariables.Expand(item.Value);
            scenario.EnvironmentVariables.TryAdd(key, value);
        }

        foreach (var (tagName, tagValue) in _configuration.Tags)
        {
            var key = _templateVariables.Expand(tagName);
            if (tagValue is string strTagValue)
            {
                scenario.Tags.TryAdd(key, _templateVariables.Expand(strTagValue));
            }
            else if (tagValue is JsonElement jsonTagValue)
            {
                if (jsonTagValue.ValueKind == JsonValueKind.String)
                {
                    scenario.Tags.TryAdd(key, _templateVariables.Expand(jsonTagValue.GetString() ?? jsonTagValue.GetRawText()));
                }
                else if (jsonTagValue.ValueKind == JsonValueKind.Number)
                {
                    scenario.Tags.TryAdd(key, jsonTagValue.GetDouble());
                }
                else if (jsonTagValue.ValueKind == JsonValueKind.True || jsonTagValue.ValueKind == JsonValueKind.False)
                {
                    scenario.Tags.TryAdd(key, jsonTagValue.GetBoolean());
                }
                else
                {
                    scenario.Tags.TryAdd(key, tagValue);
                }
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
                ref var sHookValue = ref CollectionsMarshal.GetValueRefOrAddDefault(scenario.EnvironmentVariables, Constants.StartupHookEnvironmentVariable, out var exists);
                if (exists)
                {
                    sHookValue = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ?
                        $"{startupHookLocation};{sHookValue}" :
                        $"{startupHookLocation}:{sHookValue}";
                }
                else
                {
                    sHookValue = startupHookLocation;
                }

                if (!string.IsNullOrEmpty(_configuration.MetricsProcessName))
                {
                    scenario.EnvironmentVariables[Constants.TimeItMetricsProcessName] =
                        _configuration.MetricsProcessName;
                }

                if (_configuration.MetricsFrequencyInMs != 200 && _configuration.MetricsFrequencyInMs > 0)
                {
                    scenario.EnvironmentVariables[Constants.TimeItMetricsFrequency] =
                        _configuration.MetricsFrequencyInMs.ToString();
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
        var scenarioStartArgs = new TimeItCallbacks.ScenarioStartArg(scenario);
        _callbacksTriggers.ScenarioStart(scenarioStartArgs);
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
                    Data = [],
                    Durations = [],
                    Outliers = [],
                    Metrics = [],
                    MetricsData = [],
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

        AnsiConsole.MarkupLine("  [purple_1]Cmd:[/] {0} {1}", scenario.ProcessName ?? string.Empty, scenario.ProcessArguments ?? string.Empty);
        watch = Stopwatch.StartNew();
        if (_configuration.WarmUpCount > 0)
        {
            AnsiConsole.Markup("  [gold3_1]Warming up[/]");
            watch.Restart();
            await RunScenarioAsync(_configuration.WarmUpCount, index, scenario, TimeItPhase.WarmUp, false,
                stopwatch: watch,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            watch.Stop();
            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            AnsiConsole.MarkupLine("    Duration: {0}", watch.Elapsed.ToDurationString());
        }

        AnsiConsole.Markup("  [green3]Run[/]");
        var start = DateTime.UtcNow;
        watch.Restart();
        var dataPoints = await RunScenarioAsync(_configuration.Count, index, scenario, TimeItPhase.Run, true,
            stopwatch: watch,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        watch.Stop();
        if (cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        watch.Stop();
        AnsiConsole.MarkupLine("    Duration: {0}", watch.Elapsed.ToDurationString());

        foreach (var repeat in scenarioStartArgs.GetRepeats())
        {
            AnsiConsole.Markup("  [green3]Run for '{0}'[/]", repeat.ServiceAskingForRepeat.Name);
            scenario.ParentService = repeat.ServiceAskingForRepeat;
            watch.Restart();
            await RunScenarioAsync(repeat.Count, index, scenario, TimeItPhase.ExtraRun, false,
                stopwatch: watch,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            watch.Stop();
            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            AnsiConsole.MarkupLine("    Duration: {0}", watch.Elapsed.ToDurationString());
        }
        
        scenario.ParentService = null;

        AnsiConsole.WriteLine();

        var lastStandardOutput = string.Empty;
        var durations = new List<double>();
        var metricsData = new Dictionary<string, List<double>>();
        var anyPassedDataPoint = dataPoints.Any(d => d.Status == Status.Passed);
        foreach (var item in dataPoints)
        {
            if (!string.IsNullOrEmpty(item.StandardOutput))
            {
                lastStandardOutput = item.StandardOutput;
            }

            if (item.Status == Status.Passed ||
                _configuration.ProcessFailedDataPoints ||
                _configuration.DebugMode || 
                !anyPassedDataPoint)
            {
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
        }

        // Get outliers
        var newDurations = new List<double>();
        var outliers = new List<double>();
        var threshold = 0.4d;
        var peakCount = 0;
        var isBimodal = false;
        while (threshold < 2.0d)
        {
            newDurations = Utils.RemoveOutliers(durations, threshold).ToList();
            outliers = durations.Where(d => !newDurations.Contains(d)).ToList();
            isBimodal = Utils.IsBimodal(CollectionsMarshal.AsSpan(newDurations), out peakCount, 11);
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
        var ci99 = Utils.CalculateConfidenceInterval(mean, stderr, newDurations.Count, 0.99);
        var ci95 = Utils.CalculateConfidenceInterval(mean, stderr, newDurations.Count, 0.95);
        var ci90 = Utils.CalculateConfidenceInterval(mean, stderr, newDurations.Count, 0.90);

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

        var firstResult = CreateScenarioResult(new AssertResponse(Status.Passed));
        var assertResponse = ScenarioAssertion(firstResult);
        var scenarioResult = CreateScenarioResult(assertResponse);
        scenarioResult.AdditionalMetrics = firstResult.AdditionalMetrics;
        scenarioResult.Tags = firstResult.Tags;
        scenarioResult.Metrics = firstResult.Metrics;
        _callbacksTriggers.ScenarioFinish(scenarioResult);
        return scenarioResult;
        
        ScenarioResult CreateScenarioResult(AssertResponse response)
        {
            return new ScenarioResult
            {
                Scenario = scenario,
                Count = dataPoints.Count,
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
                Ci99 = ci99,
                Ci95 = ci95,
                Ci90 = ci90,
                IsBimodal = isBimodal,
                PeakCount = peakCount,
                Metrics = metricsStats,
                MetricsData = metricsData,
                Start = start,
                End = start + watch.Elapsed,
                Duration = watch.Elapsed,
                Error = response.Message,
                Name = scenario.Name,
                ProcessName = scenario.ProcessName,
                ProcessArguments = scenario.ProcessArguments,
                EnvironmentVariables = scenario.EnvironmentVariables,
                PathValidations = scenario.PathValidations,
                WorkingDirectory = scenario.WorkingDirectory,
                Timeout = scenario.Timeout,
                Tags = scenario.Tags,
                Status = response.Status,
                OutliersThreshold = threshold,
                LastStandardOutput = lastStandardOutput,
            };
        }
    }

    private async Task<List<DataPoint>> RunScenarioAsync(int count, int index, Scenario scenario, TimeItPhase phase, bool checkShouldContinue, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        var minIterations = count / 2.5;
        minIterations = minIterations < 10 ? 10 : minIterations;
        var confidenceLevel = _configuration.ConfidenceLevel;
        if (confidenceLevel is <= 0 or >= 1)
        {
            confidenceLevel = 0.95;
        }
        var previousRelativeWidth = double.MaxValue;

        var dataPoints = new List<DataPoint>();
        AnsiConsole.Markup(" ");
        for (var i = 0; i < count; i++)
        {
            var currentRun = await RunCommandAsync(index, scenario, phase, i, cancellationToken).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
            {
                AnsiConsole.Markup("[red]cancelled[/]");
                break;
            }

            dataPoints.Add(currentRun);
            AnsiConsole.Markup(currentRun.Status == Status.Failed ? "[red]x[/]" : "[green].[/]");
            if (_configuration.DebugMode)
            {
                AnsiConsole.WriteLine();
            }

            if (checkShouldContinue && !currentRun.ShouldContinue)
            {
                break;
            }

            try
            {
                // If we are in a run phase, let's do the automatic checks
                if (phase == TimeItPhase.Run)
                {
                    static double GetDuration(DataPoint point)
                    {
#if NET7_0_OR_GREATER
                        return point.Duration.TotalNanoseconds;
#else
                        return Utils.FromTimeSpanToNanoseconds(point.Duration);
#endif
                    }

                    var durations = Utils.RemoveOutliers(dataPoints.Select(GetDuration), threshold: 1.5).ToList();
                    if (durations.Count >= minIterations || stopwatch.Elapsed.TotalMinutes >= _remainingTimeInMinutes)
                    {
                        var mean = durations.Average();
                        var stdev = durations.StandardDeviation();
                        var stderr = stdev / Math.Sqrt(durations.Count);

                        // Critical t value
                        var tCritical = StudentT.InvCDF(0, 1, durations.Count - 1, 1 - (1 - confidenceLevel) / 2);

                        // Confidence intervals
                        var marginOfError = tCritical * stderr;
                        var confidenceIntervalLower = mean - marginOfError;
                        var confidenceIntervalUpper = mean + marginOfError;
                        var relativeWidth = (confidenceIntervalUpper - confidenceIntervalLower) / mean;

                        // Check if the maximum duration is reached
                        if (stopwatch.Elapsed.TotalMinutes >= _remainingTimeInMinutes)
                        {
                            AnsiConsole.WriteLine();
                            AnsiConsole.MarkupLine(
                                "    [blueviolet]Maximum duration has been reached. Stopping iterations for this scenario.[/]");
                            AnsiConsole.MarkupLine("    [blueviolet]N: {0}[/]", durations.Count);
                            AnsiConsole.MarkupLine("    [blueviolet]Mean: {0}ms[/]",
                                Math.Round(Utils.FromNanosecondsToMilliseconds(mean), 3));
                            AnsiConsole.Markup(
                                "    [blueviolet]Confidence Interval at {0}: [[{1}ms, {2}ms]]. Relative width: {3}%[/]",
                                confidenceLevel * 100,
                                Math.Round(Utils.FromNanosecondsToMilliseconds(confidenceIntervalLower), 3),
                                Math.Round(Utils.FromNanosecondsToMilliseconds(confidenceIntervalUpper), 3),
                                Math.Round(relativeWidth * 100, 4));

                            break;
                        }

                        // Check if the statistical criterion is met
                        if (relativeWidth < _configuration.AcceptableRelativeWidth)
                        {
                            AnsiConsole.WriteLine();
                            AnsiConsole.MarkupLine(
                                "    [blueviolet]Acceptable relative width criteria met. Stopping iterations for this scenario.[/]");
                            AnsiConsole.MarkupLine("    [blueviolet]N: {0}[/]", durations.Count);
                            AnsiConsole.MarkupLine("    [blueviolet]Mean: {0}ms[/]",
                                Math.Round(Utils.FromNanosecondsToMilliseconds(mean), 3));
                            AnsiConsole.Markup(
                                "    [blueviolet]Confidence Interval at {0}: [[{1}ms, {2}ms]]. Relative width: {3}%[/]",
                                confidenceLevel * 100,
                                Math.Round(Utils.FromNanosecondsToMilliseconds(confidenceIntervalLower), 3),
                                Math.Round(Utils.FromNanosecondsToMilliseconds(confidenceIntervalUpper), 3),
                                Math.Round(relativeWidth * 100, 4));
                            break;
                        }

                        // Check for each `evaluationInterval` iteration
                        if ((durations.Count - minIterations) % _configuration.EvaluationInterval == 0)
                        {
                            var errorReduction = (previousRelativeWidth - relativeWidth) / previousRelativeWidth;
                            if (errorReduction > 0 && errorReduction < _configuration.MinimumErrorReduction)
                            {
                                AnsiConsole.WriteLine();
                                AnsiConsole.MarkupLine(
                                    "    [blueviolet]The error is not decreasing significantly. Stopping iterations for this scenario.[/]");
                                AnsiConsole.MarkupLine("    [blueviolet]N: {0}[/]", durations.Count);
                                AnsiConsole.MarkupLine("    [blueviolet]Mean: {0}ms[/]",
                                    Math.Round(Utils.FromNanosecondsToMilliseconds(mean), 3));
                                AnsiConsole.MarkupLine(
                                    "    [blueviolet]Confidence Interval at {0}: [[{1}ms, {2}ms]]. Relative width: {3}%[/]",
                                    confidenceLevel * 100,
                                    Math.Round(Utils.FromNanosecondsToMilliseconds(confidenceIntervalLower), 3),
                                    Math.Round(Utils.FromNanosecondsToMilliseconds(confidenceIntervalUpper), 3),
                                    Math.Round(relativeWidth * 100, 4));
                                AnsiConsole.Markup("    [blueviolet]Error reduction: {0}%. Minimal expected: {1}%[/]",
                                    Math.Round(errorReduction * 100, 4),
                                    Math.Round(_configuration.MinimumErrorReduction * 100, 4));

                                break;
                            }

                            previousRelativeWidth = relativeWidth;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("    [red]Error: {0}[/]", ex.Message);
                break;
            }
        }

        AnsiConsole.WriteLine();

        if (phase == TimeItPhase.Run)
        {
            _remainingTimeInMinutes -= (int)stopwatch.Elapsed.TotalMinutes;
        }

        return dataPoints;
    }

    private async Task<DataPoint> RunCommandAsync(int index, Scenario scenario, TimeItPhase phase, int executionId, CancellationToken cancellationToken)
    {
        // Prepare variables
        var cmdString = scenario.ProcessName ?? string.Empty;
        var cmdArguments = scenario.ProcessArguments ?? string.Empty;
        var workingDirectory = scenario.WorkingDirectory ?? string.Empty;
        var cmdTimeout = scenario.Timeout.MaxDuration;
        var timeoutCmdString = scenario.Timeout.ProcessName ?? string.Empty;
        var timeoutCmdArguments = scenario.Timeout.ProcessArguments ?? string.Empty;

        var cmdEnvironmentVariables = new Dictionary<string, string?>();
        foreach (DictionaryEntry osEnv in EnvironmentVariables)
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
        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            var currentPath = Environment.GetEnvironmentVariable("PATH");
            if (currentPath != null && !currentPath.Contains(workingDirectory))
            {
                var pathWithWorkingDir = workingDirectory + Path.PathSeparator + currentPath;
                Environment.SetEnvironmentVariable("PATH", pathWithWorkingDir);
            }
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

        if ((executionId == 0 && _configuration.ShowStdOutForFirstRun) || _configuration.DebugMode)
        {
            AnsiConsole.WriteLine();
            if (_configuration.DebugMode)
            {
                AnsiConsole.Markup("    [aqua]{0}. Running:[/] ", executionId + 1);
            }
            else
            {
                AnsiConsole.Markup("    [aqua]Running:[/] ");
            }
            AnsiConsole.WriteLine("{0} {1}", cmdString, cmdArguments);
            if (!string.IsNullOrWhiteSpace(workingDirectory))
            {
                AnsiConsole.Markup("    [aqua]Working Folder:[/] ");
                AnsiConsole.WriteLine(workingDirectory);
            }

            AnsiConsole.WriteLine(new string('-', 80));
            cmd = cmd.WithStandardOutputPipe(PipeTarget.Merge(cmd.StandardOutputPipe,
                PipeTarget.ToStream(Console.OpenStandardOutput())));
            cmd = cmd.WithStandardErrorPipe(PipeTarget.Merge(cmd.StandardErrorPipe,
                PipeTarget.ToStream(Console.OpenStandardError())));
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
            BufferedCommandResult? cmdResult = null;
            dataPoint.Start = DateTime.UtcNow;
            try
            {
                cmdResult = await cmd.ExecuteBufferedAsync(cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                dataPoint.End = DateTime.UtcNow;
                dataPoint.Duration = cmdResult.RunTime;
                dataPoint.Start = dataPoint.End - dataPoint.Duration;
                dataPoint.StandardOutput = cmdResult.StandardOutput;
            }
            catch (Win32Exception wEx)
            {
                Exception ex = wEx;
                while (ex.InnerException is not null)
                {
                    ex = ex.InnerException;
                }
                
                dataPoint.End = DateTime.UtcNow;
                dataPoint.Duration = dataPoint.End - dataPoint.Start;
                dataPoint.Error = ex.Message;
            }
            catch (Exception ex) when (ex is TaskCanceledException or OperationCanceledException)
            {
                dataPoint.End = DateTime.UtcNow;
                dataPoint.Duration = dataPoint.End - dataPoint.Start;
                dataPoint.Error = "Execution cancelled.";
            }
            
            ExecuteAssertions(index, scenario.Name, phase, dataPoint, cmdResult);
        }
        else
        {
            BufferedCommandResult? cmdResult = null;
            CancellationTokenSource? timeoutCts = null;
            var cmdCts = new CancellationTokenSource();
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cmdCts.Token);
            dataPoint.Start = DateTime.UtcNow;
            CommandTask<BufferedCommandResult>? cmdTask = null;

            try
            {
                dataPoint.Start = DateTime.UtcNow;
                cmdTask = cmd.ExecuteBufferedAsync(linkedCts.Token);
            }
            catch (Win32Exception wEx)
            {
                Exception ex = wEx;
                while (ex.InnerException is not null)
                {
                    ex = ex.InnerException;
                }

                dataPoint.End = DateTime.UtcNow;
                dataPoint.Duration = dataPoint.End - dataPoint.Start;
                dataPoint.Error = ex.Message;
            }
            catch (Exception ex) when (ex is OperationCanceledException)
            {
                dataPoint.End = DateTime.UtcNow;
                dataPoint.Duration = dataPoint.End - dataPoint.Start;
                dataPoint.Error = "Execution cancelled.";
            }

            if (cmdTask is not null)
            {
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
                    cmdResult = await cmdTask.ConfigureAwait(false);
                    dataPoint.End = DateTime.UtcNow;
                    timeoutCts?.Cancel();
                    dataPoint.Duration = cmdResult.RunTime;
                    dataPoint.Start = dataPoint.End - dataPoint.Duration;
                    dataPoint.StandardOutput = cmdResult.StandardOutput;
                }
                catch (Win32Exception wEx)
                {
                    Exception ex = wEx;
                    while (ex.InnerException is not null)
                    {
                        ex = ex.InnerException;
                    }

                    dataPoint.End = DateTime.UtcNow;
                    dataPoint.Duration = dataPoint.End - dataPoint.Start;
                    dataPoint.Error = ex.Message;
                }
                catch (Exception ex) when (ex is TaskCanceledException or OperationCanceledException)
                {
                    dataPoint.End = DateTime.UtcNow;
                    dataPoint.Duration = dataPoint.End - dataPoint.Start;
                    if (cancellationToken.IsCancellationRequested)
                    {
                        dataPoint.Error = "Execution cancelled.";
                    }
                    else
                    {
                        dataPoint.Error = "Process timeout.";
                    }
                }
            }

            ExecuteAssertions(index, scenario.Name, phase, dataPoint, cmdResult);
        }

        // Write metrics
        if (cmdEnvironmentVariables.TryGetValue(Constants.TimeItMetricsTemporalPathEnvironmentVariable,
                out var metricsFilePath) && !string.IsNullOrEmpty(metricsFilePath) && File.Exists(metricsFilePath))
        {
            DateTime? inProcStartDate = null;
            DateTime? inProcMainStartDate = null;
            DateTime? inProcMainEndDate = null;
            DateTime? inProcEndDate = null;
            var metrics = new Dictionary<string, double>();
            var metricsCount = new Dictionary<string, int>();

            await using (var file = File.Open(metricsFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new BinaryReader(file))
            {
                while (file.Position < file.Length)
                {
                    // Read magic number
                    if (reader.ReadInt32() != 7248)
                    {
                        continue;
                    }

                    // Read metric type
                    var type = (BinaryFileStorage.MetricType)reader.ReadByte();
                    // Read name length
                    var nameLength = reader.ReadInt32();
                    // Read name
                    var nameBytes = reader.ReadBytes(nameLength);
                    var name = Encoding.UTF8.GetString(nameBytes);
                    // Read value
                    var value = reader.ReadDouble();

                    try
                    {
                        if (name is not null)
                        {
                            static void EnsureMainDuration(Dictionary<string, double> values,
                                DateTime? mainStartDate, DateTime? mainEndDate)
                            {
                                if (mainStartDate is not null && mainEndDate is not null)
                                {
                                    values[Constants.ProcessInternalDurationMetricNameString] =
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
                                    values[Constants.ProcessStartupHookOverheadMetricNameString] = overheadDuration;
                                    values[Constants.ProcessCorrectedDurationMetricNameString] =
                                        globalDuration - overheadDuration;
                                }
                            }

                            if (name == Constants.ProcessStartTimeUtcMetricNameString)
                            {
                                inProcStartDate = DateTime.FromBinary((long)value);
                                metrics[Constants.ProcessTimeToStartMetricNameString] =
                                    (inProcStartDate.Value - dataPoint.Start).TotalMilliseconds;
                                EnsureStartupHookOverhead(dataPoint, metrics, inProcStartDate, inProcMainStartDate,
                                    inProcMainEndDate, inProcEndDate);
                                continue;
                            }

                            if (name == Constants.MainMethodStartTimeUtcMetricNameString)
                            {
                                inProcMainStartDate = DateTime.FromBinary((long)value);
                                metrics[Constants.ProcessTimeToMainMetricNameString] =
                                    (inProcMainStartDate.Value - dataPoint.Start).TotalMilliseconds;
                                EnsureMainDuration(metrics, inProcMainStartDate, inProcMainEndDate);
                                EnsureStartupHookOverhead(dataPoint, metrics, inProcStartDate, inProcMainStartDate,
                                    inProcMainEndDate, inProcEndDate);
                                continue;
                            }

                            if (name == Constants.MainMethodEndTimeUtcMetricNameString)
                            {
                                inProcMainEndDate = DateTime.FromBinary((long)value);
                                metrics[Constants.ProcessTimeToMainEndMetricNameString] =
                                    (dataPoint.End - inProcMainEndDate.Value).TotalMilliseconds;
                                EnsureMainDuration(metrics, inProcMainStartDate, inProcMainEndDate);
                                EnsureStartupHookOverhead(dataPoint, metrics, inProcStartDate, inProcMainStartDate,
                                    inProcMainEndDate, inProcEndDate);
                                continue;
                            }

                            if (name == Constants.ProcessEndTimeUtcMetricNameString)
                            {
                                inProcEndDate = DateTime.FromBinary((long)value);
                                metrics[Constants.ProcessTimeToEndMetricNameString] =
                                    (dataPoint.End - inProcEndDate.Value).TotalMilliseconds;
                                EnsureStartupHookOverhead(dataPoint, metrics, inProcStartDate, inProcMainStartDate,
                                    inProcMainEndDate, inProcEndDate);
                                continue;
                            }

                            if (type == BinaryFileStorage.MetricType.Counter)
                            {
                                metrics[name] = value;
                            }
                            else if (type is BinaryFileStorage.MetricType.Gauge or BinaryFileStorage.MetricType.Timer)
                            {
                                ref var oldValue = ref CollectionsMarshal.GetValueRefOrAddDefault(metrics, name, out _);
                                oldValue += value;

                                ref var count =
                                    ref CollectionsMarshal.GetValueRefOrAddDefault(metricsCount, name, out _);
                                count++;
                            }
                            else if (type == BinaryFileStorage.MetricType.Increment)
                            {
                                ref var oldValue = ref CollectionsMarshal.GetValueRefOrAddDefault(metrics, name, out _);
                                oldValue += value;
                            }
                        }
                    }
                    catch
                    {
                        // Error reading metric item, we just skip that item
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

        _callbacksTriggers.ExecutionEnd(dataPoint, phase);

        if ((executionId == 0 && _configuration.ShowStdOutForFirstRun) || _configuration.DebugMode)
        {
            AnsiConsole.WriteLine(new string('-', 80));
            AnsiConsole.Write("   ");
            if (_configuration.DebugMode)
            {
                AnsiConsole.Markup(" [aqua]Result:[/] ");
            }
        }
        
        return dataPoint;
    }

    private void ExecuteAssertions(int scenarioId, string scenarioName, TimeItPhase phase, DataPoint dataPoint, BufferedCommandResult? cmdResult)
    {
        var exitCode = cmdResult?.ExitCode ?? -1;
        var standardOutput = cmdResult?.StandardOutput ?? string.Empty;
        var standardError = cmdResult?.StandardError ?? dataPoint.Error;
        var assertionData = new AssertionData(scenarioId, scenarioName, phase, dataPoint.Start, dataPoint.End,
            dataPoint.Duration, exitCode, standardOutput, standardError, _services);
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

    private AssertResponse ScenarioAssertion(ScenarioResult scenarioResult)
    {
        if (_assertors is null || _assertors.Count == 0)
        {
            return new AssertResponse(Status.Passed);
        }

        var status = Status.Passed;
        var shouldContinue = true;
        HashSet<string>? messagesHashSet = null;
        foreach (var assertor in _assertors)
        {
            if (assertor is null)
            {
                continue;
            }

            var result = assertor.ScenarioAssertion(scenarioResult);
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

        var message = string.Empty;
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

        var status = Status.Passed;
        var shouldContinue = true;
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

        var message = string.Empty;
        if (messagesHashSet?.Count > 0)
        {
            message = string.Join(Environment.NewLine, messagesHashSet);
        }

        return new AssertResponse(status, shouldContinue, message);
    }
}
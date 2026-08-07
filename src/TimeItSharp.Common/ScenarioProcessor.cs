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

    internal bool HasLifecycleErrors { get; private set; }

    // Capture the host environment for this engine run. Each ScenarioProcessor is created per
    // RunAsync invocation, so repeated runs in a long-lived host observe the current environment
    // without ever mutating Environment or sharing a mutable dictionary between runs.
    private readonly IReadOnlyDictionary<string, string?> _environmentVariables;
    private readonly IReadOnlyList<string> _knownSecretValues;

    private TimeSpan _remainingDuration;
    
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
        var environmentVariables = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (DictionaryEntry environmentVariable in Environment.GetEnvironmentVariables())
        {
            if (environmentVariable.Key?.ToString() is { Length: > 0 } key)
            {
                environmentVariables[key] = environmentVariable.Value?.ToString();
            }
        }

        _environmentVariables = environmentVariables;
        _knownSecretValues = Utils.GetSensitiveEnvironmentValues(configuration.EnvironmentVariables)
            .Concat(Utils.GetTemplateSecretValues(templateVariables))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        _remainingDuration = configuration.MaximumDurationInMinutes > 0
            ? TimeSpan.FromMinutes(configuration.MaximumDurationInMinutes)
            : TimeSpan.Zero;
    }

    [UnconditionalSuppressMessage("SingleFile", "IL3000:Avoid accessing Assembly file path when publishing as a single file", Justification = "Case is being handled")]
    public void PrepareScenario(Scenario scenario)
    {
        if (string.IsNullOrWhiteSpace(scenario.ProcessName))
        {
            scenario.ProcessName = _configuration.ProcessName;
        }

        scenario.ProcessName = _templateVariables.Expand(scenario.ProcessName ?? string.Empty);

        if (string.IsNullOrEmpty(scenario.ProcessArguments))
        {
            scenario.ProcessArguments = _configuration.ProcessArguments;
        }

        scenario.ProcessArguments = _templateVariables.Expand(scenario.ProcessArguments ?? string.Empty);

        if (string.IsNullOrWhiteSpace(scenario.WorkingDirectory))
        {
            scenario.WorkingDirectory = _configuration.WorkingDirectory;
        }

        scenario.WorkingDirectory = _templateVariables.Expand(scenario.WorkingDirectory ?? string.Empty);

        var expandedScenarioEnvironmentVariables = scenario.EnvironmentVariables
            .Select(item => new
            {
                Key = _templateVariables.Expand(item.Key),
                Value = _templateVariables.Expand(item.Value),
            })
            .ToList();
        scenario.EnvironmentVariables.Clear();
        foreach (var item in expandedScenarioEnvironmentVariables)
        {
            scenario.EnvironmentVariables[item.Key] = item.Value;
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
            // Resolve the hook by its packaged on-disk contract rather than touching a netcoreapp3.1
            // type from a trimmed/single-file host. Loading that legacy assembly only in the target
            // process avoids a host-side System.Runtime binding failure.
            var startupHookAssemblyLocation = GetStartupHookAssemblyLocation();

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

        if (string.IsNullOrWhiteSpace(scenario.Timeout.ProcessName))
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
        scenario.ParentService = null;
    }

    public async Task<ScenarioResult?> ProcessScenarioAsync(int index, Scenario scenario, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        // A pre-cancelled run does not start a scenario. Once ScenarioStart has completed, every
        // path below produces one result and one ScenarioFinish notification, including
        // cancellation and callback failures.
        var scenarioStartArgs = new TimeItCallbacks.ScenarioStartArg(scenario);
        var scenarioStarted = false;
        ScenarioResult? result = null;
        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            scenarioStarted = true;
            _callbacksTriggers.ScenarioStart(scenarioStartArgs);
            result = await ProcessScenarioCoreAsync(index, scenario, scenarioStartArgs, cancellationToken)
                .ConfigureAwait(false);
            if (result is null)
            {
                result = CreateFailedScenarioResult(scenario, "Execution cancelled.", []);
            }
        }
        catch (OperationCanceledException)
        {
            HasLifecycleErrors = true;
            result = CreateFailedScenarioResult(scenario, "Execution cancelled.");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            HasLifecycleErrors = true;
            result = CreateFailedScenarioResult(scenario, ex.Message);
        }
        finally
        {
            // A service may request extra runs by assigning ParentService. It must never leak to
            // another scenario, even when a command, callback, or cancellation throws.
            scenario.ParentService = null;
            if (scenarioStarted)
            {
                result ??= CreateFailedScenarioResult(scenario, cancellationToken.IsCancellationRequested
                    ? "Execution cancelled."
                    : "Scenario execution failed.");
                try
                {
                    _callbacksTriggers.ScenarioFinish(result);
                }
                catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
                {
                    HasLifecycleErrors = true;
                    result.Status = Status.Failed;
                    result.Error = string.IsNullOrEmpty(result.Error)
                        ? ex.Message
                        : result.Error + Environment.NewLine + ex.Message;
                }
            }
        }

        return result;
    }

    private async Task<ScenarioResult?> ProcessScenarioCoreAsync(
        int index,
        Scenario scenario,
        TimeItCallbacks.ScenarioStartArg scenarioStartArgs,
        CancellationToken cancellationToken)
    {
        Stopwatch? watch = null;
        AnsiConsole.MarkupLine(
            "[dodgerblue1]Scenario:[/] {0}",
            Utils.EscapeMarkup(Utils.SanitizeText(scenario.Name, _knownSecretValues)));

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
                return CreateFailedScenarioResult(scenario, validationErrors);
            }
        }

        AnsiConsole.MarkupLine(
            "  [purple_1]Cmd:[/] {0} {1}",
            Utils.EscapeMarkup(Utils.SanitizeText(scenario.ProcessName, _knownSecretValues)),
            Utils.EscapeMarkup(Utils.SanitizeText(scenario.ProcessArguments, _knownSecretValues)));
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
        var scenarioStopwatch = Stopwatch.StartNew();
        watch.Restart();
        var dataPoints = await RunScenarioAsync(_configuration.Count, index, scenario, TimeItPhase.Run, true,
            stopwatch: watch,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        watch.Stop();
        if (cancellationToken.IsCancellationRequested)
        {
            return CreateFailedScenarioResult(scenario, "Execution cancelled.", dataPoints);
        }

        watch.Stop();
        AnsiConsole.MarkupLine("    Duration: {0}", watch.Elapsed.ToDurationString());

        foreach (var repeat in scenarioStartArgs.GetRepeats())
        {
            AnsiConsole.Markup(
                "  [green3]Run for '{0}'[/]",
                Utils.EscapeMarkup(Utils.SanitizeText(repeat.ServiceAskingForRepeat.Name, _knownSecretValues)));
            scenario.ParentService = repeat.ServiceAskingForRepeat;
            watch.Restart();
            await RunScenarioAsync(repeat.Count, index, scenario, TimeItPhase.ExtraRun, false,
                stopwatch: watch,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            watch.Stop();
            if (cancellationToken.IsCancellationRequested)
            {
                return CreateFailedScenarioResult(scenario, "Execution cancelled.", dataPoints);
            }

            AnsiConsole.MarkupLine("    Duration: {0}", watch.Elapsed.ToDurationString());
        }
        
        scenario.ParentService = null;
        scenarioStopwatch.Stop();
        var scenarioDuration = scenarioStopwatch.Elapsed;
        var scenarioEnd = start + scenarioDuration;

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
                var duration = item.Duration.TotalNanoseconds;
#else
                var duration = Utils.FromTimeSpanToNanoseconds(item.Duration);
#endif
                if (!double.IsFinite(duration))
                {
                    continue;
                }

                durations.Add(duration);
                if (item.Metrics is null)
                {
                    continue;
                }

                foreach (var kv in item.Metrics)
                {
                    if (!double.IsFinite(kv.Value))
                    {
                        continue;
                    }

                    if (!metricsData.TryGetValue(kv.Key, out var metricsItem))
                    {
                        metricsItem = new List<double>();
                        metricsData[kv.Key] = metricsItem;
                    }

                    metricsItem.Add(kv.Value);
                }
            }
        }

        if (durations.Count == 0)
        {
            return CreateFailedScenarioResult(
                scenario,
                "No valid data points were collected for this scenario.",
                dataPoints);
        }

        // Get outliers. Keep a non-empty fallback on every iteration: a very small sample can
        // legitimately have every value rejected by RemoveOutliers.
        var previousDurations = durations.ToList();
        var newDurations = new List<double>();
        var outliers = new List<double>();
        var threshold = 0.4d;
        var peakCount = 0;
        var isBimodal = false;
        while (threshold < 2.0d)
        {
            var candidate = Utils.RemoveOutliers(durations, threshold).Where(double.IsFinite).ToList();
            newDurations = candidate;
            outliers = durations.Where(d => !newDurations.Contains(d)).ToList();
            isBimodal = Utils.IsBimodal(CollectionsMarshal.AsSpan(newDurations), out peakCount, 11);
            var outliersPercent = ((double)outliers.Count / durations.Count) * 100;
            if (outliersPercent < 20 && !isBimodal)
            {
                break;
            }

            if (candidate.Count > 0)
            {
                previousDurations = candidate;
            }

            threshold += 0.1;
        }

        if (newDurations.Count == 0)
        {
            newDurations = previousDurations.Count > 0 ? previousDurations : durations.ToList();
            outliers = durations.Where(d => !newDurations.Contains(d)).ToList();
            isBimodal = Utils.IsBimodal(CollectionsMarshal.AsSpan(newDurations), out peakCount, 11);
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
        foreach (var key in metricsData.Keys.ToArray())
        {
            var originalMetricsValue = metricsData[key];
            if (originalMetricsValue.Count == 0)
            {
                continue;
            }

            var previousMetricsValue = originalMetricsValue.ToList();
            var metricsValue = new List<double>();
            var metricsOutliers = new List<double>();
            var metricsThreshold = 0.4d;
            while (metricsThreshold < 3.0d)
            {
                metricsValue = Utils.RemoveOutliers(originalMetricsValue, metricsThreshold).Where(double.IsFinite).ToList();
                metricsOutliers = originalMetricsValue.Where(d => !metricsValue.Contains(d)).ToList();
                var outliersPercent = ((double)metricsOutliers.Count / originalMetricsValue.Count) * 100;
                if (outliersPercent < 20)
                {
                    // Outliers must be not more than 20% of the data. Keep a non-empty sample
                    // when every value was rejected.
                    if (metricsValue.Count == 0)
                    {
                        metricsValue = previousMetricsValue;
                        metricsOutliers = originalMetricsValue.Where(d => !metricsValue.Contains(d)).ToList();
                    }
                    break;
                }

                if (metricsValue.Count > 0)
                {
                    previousMetricsValue = metricsValue;
                }

                metricsThreshold += 0.1;
            }

            if (metricsValue.Count == 0)
            {
                metricsValue = previousMetricsValue.Count > 0 ? previousMetricsValue : originalMetricsValue;
                metricsOutliers = originalMetricsValue.Where(d => !metricsValue.Contains(d)).ToList();
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
                End = scenarioEnd,
                Duration = scenarioDuration,
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
            if (cancellationToken.IsCancellationRequested)
            {
                AnsiConsole.Markup("[red]cancelled[/]");
                break;
            }

            var currentRun = await RunCommandAsync(index, scenario, phase, i, cancellationToken).ConfigureAwait(false);
            // A command that completed before cancellation remains a useful datapoint. A command
            // interrupted by the token is not included in statistics or the completed count.
            if (cancellationToken.IsCancellationRequested &&
                string.Equals(currentRun.Error, "Execution cancelled.", StringComparison.Ordinal))
            {
                AnsiConsole.Markup("[red]cancelled[/]");
                break;
            }

            dataPoints.Add(currentRun);
            if (cancellationToken.IsCancellationRequested)
            {
                AnsiConsole.Markup("[red]cancelled[/]");
                break;
            }
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
                    if (durations.Count >= minIterations || stopwatch.Elapsed >= _remainingDuration)
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
                        var relativeWidth = mean == 0
                            ? (confidenceIntervalUpper == confidenceIntervalLower ? 0 : double.PositiveInfinity)
                            : (confidenceIntervalUpper - confidenceIntervalLower) / mean;
                        if (!double.IsFinite(relativeWidth))
                        {
                            relativeWidth = double.PositiveInfinity;
                        }

                        // Check if the maximum duration is reached
                        if (stopwatch.Elapsed >= _remainingDuration)
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
                AnsiConsole.WriteException(Utils.SanitizeException(ex, _knownSecretValues));
                break;
            }
        }

        AnsiConsole.WriteLine();

        if (phase == TimeItPhase.Run)
        {
            _remainingDuration -= stopwatch.Elapsed;
            if (_remainingDuration < TimeSpan.Zero)
            {
                _remainingDuration = TimeSpan.Zero;
            }
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

        // Start from this run's immutable environment snapshot. The dictionary is always copied
        // so adding a scenario PATH or metric variable cannot mutate the host or another command.
        var cmdEnvironmentVariables = new Dictionary<string, string?>(_environmentVariables, StringComparer.Ordinal);

        foreach (var envVar in scenario.EnvironmentVariables)
        {
            cmdEnvironmentVariables[envVar.Key] = envVar.Value;
        }

        // Datadog CI visibility logs are scoped to the measured child. Never set this on the
        // host process, where it would leak into later engine runs in a long-lived host.
        if (_configuration.EnableDatadog)
        {
            cmdEnvironmentVariables["DD_CIVISIBILITY_LOGS_ENABLED"] = "true";
        }

        string? metricsFilePath = null;
        if (cmdEnvironmentVariables.ContainsKey(Constants.StartupHookEnvironmentVariable))
        {
            metricsFilePath = Path.GetTempFileName();
            cmdEnvironmentVariables[Constants.TimeItMetricsTemporalPathEnvironmentVariable] = metricsFilePath;
        }

        // Make binaries in the working directory available only to this command.
        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            var isWindows = OperatingSystem.IsWindows();
            // Environment variable names are case-insensitive on Windows but case-sensitive on
            // Unix. Never rewrite a lower-case `path` entry on Unix and compare path entries with
            // the host filesystem's semantics.
            var pathKey = isWindows
                ? cmdEnvironmentVariables.Keys.FirstOrDefault(
                    key => string.Equals(key, "PATH", StringComparison.OrdinalIgnoreCase)) ?? "PATH"
                : "PATH";
            var currentPath = cmdEnvironmentVariables.TryGetValue(pathKey, out var path)
                ? path
                : Environment.GetEnvironmentVariable(pathKey);
            var pathEntries = currentPath?.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                ?? Array.Empty<string>();
            var pathComparison = isWindows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!pathEntries.Any(entry => string.Equals(entry, workingDirectory, pathComparison)))
            {
                cmdEnvironmentVariables[pathKey] = string.IsNullOrEmpty(currentPath)
                    ? workingDirectory
                    : workingDirectory + Path.PathSeparator + currentPath;
            }

            if (!Path.IsPathRooted(cmdString))
            {
                var commandInWorkingDirectory = Path.Combine(workingDirectory, cmdString);
                if (File.Exists(commandInWorkingDirectory))
                {
                    cmdString = commandInWorkingDirectory;
                }
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
            AnsiConsole.WriteLine(
                "{0} {1}",
                Utils.SanitizeText(cmdString, _knownSecretValues),
                Utils.SanitizeText(cmdArguments, _knownSecretValues));
            if (!string.IsNullOrWhiteSpace(workingDirectory))
            {
                AnsiConsole.Markup("    [aqua]Working Folder:[/] ");
                AnsiConsole.WriteLine(Utils.SanitizeText(workingDirectory, _knownSecretValues));
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
            Start = DateTime.UtcNow,
        };

        try
        {
            _callbacksTriggers.ExecutionStart(dataPoint, phase, ref cmd);
        }
        catch
        {
            dataPoint.End = DateTime.UtcNow;
            dataPoint.Duration = dataPoint.End - dataPoint.Start;
            DeleteMetricsFile(metricsFilePath);
            // Start was attempted even though one handler failed. Give End handlers their
            // symmetric cleanup opportunity, but preserve the original Start exception.
            TryExecutionEnd(dataPoint, phase);
            DeleteMetricsFile(metricsFilePath);
            throw;
        }

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
            
            catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException)
            {
                dataPoint.End = DateTime.UtcNow;
                dataPoint.Duration = dataPoint.End - dataPoint.Start;
                dataPoint.Error = ex.Message;
            }

            try
            {
                ExecuteAssertions(index, scenario.Name, phase, dataPoint, cmdResult);
            }
            catch
            {
                DeleteMetricsFile(metricsFilePath);
                TryExecutionEnd(dataPoint, phase);
                DeleteMetricsFile(metricsFilePath);
                throw;
            }
        }
        else
        {
            BufferedCommandResult? cmdResult = null;
            using var cmdCts = new CancellationTokenSource();
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cmdCts.Token);
            using var timeoutCts = string.IsNullOrEmpty(timeoutCmdString)
                ? null
                : new CancellationTokenSource();
            dataPoint.Start = DateTime.UtcNow;
            CommandTask<BufferedCommandResult>? cmdTask = null;
            Task<bool>? timeoutTask = null;
            var targetActive = true;
            var targetGate = new object();

            try
            {
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
            catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException)
            {
                dataPoint.End = DateTime.UtcNow;
                dataPoint.Duration = dataPoint.End - dataPoint.Start;
                dataPoint.Error = ex.Message;
            }

            if (cmdTask is not null)
            {
                Action cancelTarget = () =>
                {
                    lock (targetGate)
                    {
                        if (targetActive && !cancellationToken.IsCancellationRequested)
                        {
                            cmdCts.Cancel();
                        }
                    }
                };

                if (timeoutCts is not null)
                {
                    timeoutTask = RunCommandTimeoutAsync(
                        TimeSpan.FromSeconds(cmdTimeout),
                        timeoutCmdString,
                        timeoutCmdArguments,
                        workingDirectory,
                        cmdTask.ProcessId,
                        cancelTarget,
                        timeoutCts.Token,
                        cancellationToken,
                        cmdEnvironmentVariables);
                }
                else
                {
                    cmdCts.CancelAfter(TimeSpan.FromSeconds(cmdTimeout));
                }

                try
                {
                    cmdResult = await cmdTask.ConfigureAwait(false);
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
                    dataPoint.Error = cancellationToken.IsCancellationRequested
                        ? "Execution cancelled."
                        : "Process timeout.";
                }
                catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException)
                {
                    dataPoint.End = DateTime.UtcNow;
                    dataPoint.Duration = dataPoint.End - dataPoint.Start;
                    dataPoint.Error = ex.Message;
                }
                finally
                {
                    lock (targetGate)
                    {
                        targetActive = false;
                    }

                    timeoutCts?.Cancel();
                    if (timeoutTask is not null)
                    {
                        try
                        {
                            await timeoutTask.ConfigureAwait(false);
                        }
                        catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException)
                        {
                            // The helper is supplementary and must not mask the target result.
                        }
                    }
                }
            }

            try
            {
                ExecuteAssertions(index, scenario.Name, phase, dataPoint, cmdResult);
            }
            catch
            {
                DeleteMetricsFile(metricsFilePath);
                TryExecutionEnd(dataPoint, phase);
                DeleteMetricsFile(metricsFilePath);
                throw;
            }
        }

        // Write metrics
        if (!string.IsNullOrEmpty(metricsFilePath) &&
            IsSafeMetricsFilePath(metricsFilePath) &&
            File.Exists(metricsFilePath))
        {
            DateTime? inProcStartDate = null;
            DateTime? inProcMainStartDate = null;
            DateTime? inProcMainEndDate = null;
            DateTime? inProcEndDate = null;
            var metrics = new Dictionary<string, double>();
            var metricsCount = new Dictionary<string, int>();

            try
            {
                await using (var file = File.Open(metricsFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var reader = new BinaryReader(file))
                {
                    while (file.Position + 4 <= file.Length)
                    {
                        BinaryFileStorage.MetricType type;
                        int nameLength;
                        byte[] nameBytes;
                        string name;
                        double value;

                        try
                        {
                            // Read magic number
                            if (reader.ReadInt32() != 7248)
                            {
                                continue;
                            }

                            // Read metric type
                            type = (BinaryFileStorage.MetricType)reader.ReadByte();
                            // Read name length
                            nameLength = reader.ReadInt32();
                            if (nameLength < 0 || nameLength > file.Length - file.Position - sizeof(double))
                            {
                                break;
                            }

                            // Read name
                            nameBytes = reader.ReadBytes(nameLength);
                            if (nameBytes.Length != nameLength || file.Position + sizeof(double) > file.Length)
                            {
                                break;
                            }

                            name = Encoding.UTF8.GetString(nameBytes);
                            // Read value. NaN/Infinity are not meaningful metric samples and would
                            // make the statistics and JSON exporters fail later in the pipeline.
                            value = reader.ReadDouble();
                            if (!double.IsFinite(value))
                            {
                                continue;
                            }
                        }
                        catch (EndOfStreamException)
                        {
                            // We reached the end of the stream, corrupted data, just break
                            break;
                        }

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
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Metrics are supplementary; an unavailable or partially written file must not
                // make the process execution fail.
            }
            finally
            {
                try
                {
                    if (IsSafeMetricsFilePath(metricsFilePath))
                    {
                        File.Delete(metricsFilePath);
                    }
                }
                catch
                {
                    // Do nothing
                }
            }

            foreach (var mItem in metricsCount)
            {
                metrics[mItem.Key] /= mItem.Value;
            }

            dataPoint.Metrics = metrics;
        }

        DeleteMetricsFile(metricsFilePath);
        try
        {
            _callbacksTriggers.ExecutionEnd(dataPoint, phase);
        }
        catch
        {
            DeleteMetricsFile(metricsFilePath);
            throw;
        }

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

    private ScenarioResult CreateFailedScenarioResult(
        Scenario scenario,
        string error,
        IReadOnlyList<DataPoint>? dataPoints = null)
    {
        var now = DateTime.UtcNow;
        var result = new ScenarioResult
        {
            Scenario = scenario,
            Count = dataPoints?.Count ?? _configuration.Count,
            WarmUpCount = _configuration.WarmUpCount,
            Data = dataPoints?.ToList() ?? [],
            Durations = [],
            Outliers = [],
            Mean = 0,
            Median = 0,
            Max = 0,
            Min = 0,
            Stdev = 0,
            StdErr = 0,
            P99 = 0,
            P95 = 0,
            P90 = 0,
            Ci99 = [0, 0],
            Ci95 = [0, 0],
            Ci90 = [0, 0],
            Metrics = [],
            MetricsData = [],
            Error = error,
            Name = scenario.Name,
            ProcessName = scenario.ProcessName,
            ProcessArguments = scenario.ProcessArguments,
            EnvironmentVariables = new Dictionary<string, string>(scenario.EnvironmentVariables),
            PathValidations = new List<string>(scenario.PathValidations),
            WorkingDirectory = scenario.WorkingDirectory,
            Timeout = scenario.Timeout.Clone(),
            Tags = new Dictionary<string, object>(scenario.Tags),
            Status = Status.Failed,
            OutliersThreshold = 0,
            Start = now,
            End = now,
            Duration = TimeSpan.Zero,
        };
        return result;
    }

    private void TryExecutionEnd(DataPoint dataPoint, TimeItPhase phase)
    {
        try
        {
            _callbacksTriggers.ExecutionEnd(dataPoint, phase);
        }
        catch
        {
            // Preserve the original command/assertion/start failure. The scenario lifecycle
            // wrapper records the failed result and still invokes ScenarioFinish.
        }
    }

    private static string? GetStartupHookAssemblyLocation()
    {
        var candidate = Path.Combine(AppContext.BaseDirectory, "TimeItSharp.StartupHook.dll");
        return File.Exists(candidate) ? candidate : null;
    }

    private static void DeleteMetricsFile(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            if (IsSafeMetricsFilePath(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Metrics are supplementary. Cleanup must not mask the command/callback error.
        }
    }

    private static bool IsSafeMetricsFilePath(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            var tempPath = Path.GetFullPath(Path.GetTempPath());
            var tempPrefix = tempPath.EndsWith(Path.DirectorySeparatorChar)
                ? tempPath
                : tempPath + Path.DirectorySeparatorChar;
            var comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (!fullPath.StartsWith(tempPrefix, comparison))
            {
                return false;
            }

            return (File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) == 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
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

    private async Task<bool> RunCommandTimeoutAsync(
        TimeSpan timeout,
        string timeoutCmd,
        string timeoutArgument,
        string workingDirectory,
        int targetPid,
        Action? targetCancellation,
        CancellationToken timeoutCancellationToken,
        CancellationToken applicationCancellationToken,
        IReadOnlyDictionary<string, string?> environmentVariables)
    {
        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                timeoutCancellationToken,
                applicationCancellationToken);
            await Task.Delay(timeout, linkedCts.Token).ConfigureAwait(false);
            if (linkedCts.Token.IsCancellationRequested)
            {
                return false;
            }

            var targetPidString = targetPid.ToString();
            var templateVariables = _templateVariables.Clone();
            templateVariables.Add("PID", targetPidString);

            timeoutCmd = templateVariables.Expand(timeoutCmd).Replace("%pid%", targetPidString);
            timeoutArgument = templateVariables.Expand(timeoutArgument).Replace("%pid%", targetPidString);

            if (!Path.IsPathRooted(timeoutCmd) && !string.IsNullOrWhiteSpace(workingDirectory))
            {
                var commandInWorkingDirectory = Path.Combine(workingDirectory, timeoutCmd);
                if (File.Exists(commandInWorkingDirectory))
                {
                    timeoutCmd = commandInWorkingDirectory;
                }
            }

            var cmd = Cli.Wrap(timeoutCmd)
                .WithEnvironmentVariables(new Dictionary<string, string?>(environmentVariables))
                .WithWorkingDirectory(workingDirectory)
                .WithValidation(CommandResultValidation.None);
            if (!string.IsNullOrEmpty(timeoutArgument))
            {
                cmd = cmd.WithArguments(timeoutArgument);
            }

            var cmdResult = await cmd.ExecuteBufferedAsync(linkedCts.Token).ConfigureAwait(false);
            if (cmdResult.ExitCode != 0)
            {
                if (!string.IsNullOrWhiteSpace(cmdResult.StandardError))
                {
                    AnsiConsole.WriteLine(Utils.SanitizeOutput(cmdResult.StandardError, _knownSecretValues));
                }

                if (!string.IsNullOrWhiteSpace(cmdResult.StandardOutput))
                {
                    AnsiConsole.WriteLine(Utils.SanitizeOutput(cmdResult.StandardOutput, _knownSecretValues));
                }

                return false;
            }

            // Only a helper that actually ran after the delay may cancel the target. The active
            // gate in RunCommandAsync prevents a race with normal target completion.
            targetCancellation?.Invoke();
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException)
        {
            AnsiConsole.WriteException(Utils.SanitizeException(ex, _knownSecretValues));
            return false;
        }
    }

    private AssertResponse ScenarioAssertion(ScenarioResult scenarioResult)
    {
        if (_assertors.Count == 0)
        {
            return new AssertResponse(Status.Passed);
        }

        var status = Status.Passed;
        var shouldContinue = true;
        HashSet<string>? messagesHashSet = null;
        foreach (var assertor in _assertors)
        {
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
        if (_assertors.Count == 0)
        {
            return new AssertResponse(Status.Passed);
        }

        var status = Status.Passed;
        var shouldContinue = true;
        HashSet<string>? messagesHashSet = null;
        foreach (var assertor in _assertors)
        {
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

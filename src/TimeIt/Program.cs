using System.Collections;
using System.Diagnostics;
using CliWrap;
using CliWrap.Buffered;
using MathNet.Numerics.Statistics;
using Spectre.Console;
using TimeIt;
using TimeIt.Common.Configuration;
using TimeIt.Common.Exporter;
using TimeIt.Common.Results;

AnsiConsole.MarkupLine("[bold aqua]TimeIt by Tony Redondo[/]\n");

// Check arguments
if (args.Length < 1)
{
    AnsiConsole.MarkupLine("[red]Missing argument with the configuration file.[/]");
    Environment.Exit(1);
    return;
}

// Load configuration
var config = Config.LoadConfiguration(args[0]);
config.JsonExporterFilePath = Utils.ReplaceCustomVars(config.JsonExporterFilePath);

// Enable exporters
var exporters = new List<IExporter>();
exporters.Add(new JsonExporter());

AnsiConsole.MarkupLine("[blue1]Warmup count:[/] {0}", config.WarmUpCount);
AnsiConsole.MarkupLine("[blue1]Count:[/] {0}", config.WarmUpCount);
AnsiConsole.MarkupLine("[blue1]Number of Scenarios:[/] {0}", config.Scenarios.Count);
AnsiConsole.MarkupLine("[blue1]Exporters:[/] {0}", string.Join(", ", exporters.Select(e => e.Name)));
AnsiConsole.WriteLine();

var scenariosResults = new List<ScenarioResult>();
var scenarioWithErrors = 0;
if (config is { Count: > 0, Scenarios.Count: > 0 })
{
    foreach (var scenario in config.Scenarios)
    {
        // Prepare scenario
        PrepareScenario(scenario, config);
        
        // Process scenario
        var result = await ProcessScenarioAsync(scenario, config).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(result.Error))
        {
            scenarioWithErrors++;
        }

        scenariosResults.Add(result);
    }
    
    if (scenarioWithErrors < config.Scenarios.Count)
    {
	    // Print results in a table
	    // TODO: print results in a table
	    
	    // Export data
	    foreach (var exporter in exporters)
	    {
		    exporter.SetConfiguration(config);
		    if (exporter.Enabled)
		    {
			    exporter.Export(scenariosResults);
		    }
	    }
    }
    else
    {
	    for (var i = 0; i < scenariosResults.Count; i++)
	    {
		    if (!string.IsNullOrEmpty(scenariosResults[i].Error))
		    {
			    AnsiConsole.MarkupLine("Error in Scenario: {0}", i);
			    AnsiConsole.WriteLine(scenariosResults[i].Error);
		    }
	    }

	    Environment.Exit(1);
    }
}

return;

static void PrepareScenario(Scenario scenario, Config configuration)
{
    if (string.IsNullOrEmpty(scenario.ProcessName))
    {
        scenario.ProcessName = configuration.ProcessName;
    }

    scenario.ProcessName = Utils.ReplaceCustomVars(scenario.ProcessName ?? string.Empty);
    
    if (string.IsNullOrEmpty(scenario.ProcessArguments))
    {
        scenario.ProcessArguments = configuration.ProcessArguments;
    }

    scenario.ProcessArguments = Utils.ReplaceCustomVars(scenario.ProcessArguments ?? string.Empty);
    
    if (string.IsNullOrEmpty(scenario.WorkingDirectory))
    {
        scenario.WorkingDirectory = configuration.WorkingDirectory;
    }

    scenario.WorkingDirectory = Utils.ReplaceCustomVars(scenario.WorkingDirectory ?? string.Empty);

    foreach (var item in scenario.EnvironmentVariables)
    {
        scenario.EnvironmentVariables[item.Key] = Utils.ReplaceCustomVars(item.Value);
    }

    foreach (var item in configuration.EnvironmentVariables)
    {
        var value = Utils.ReplaceCustomVars(item.Value);
        if (!scenario.EnvironmentVariables.ContainsKey(item.Key))
        {
            scenario.EnvironmentVariables[item.Key] = value;
        }
    }

    if (scenario.Timeout.MaxDuration <= 0 && configuration.Timeout.MaxDuration > 0)
    {
        scenario.Timeout.MaxDuration = configuration.Timeout.MaxDuration;
    }

    if (string.IsNullOrEmpty(scenario.Timeout.ProcessName))
    {
        scenario.Timeout.ProcessName = configuration.Timeout.ProcessName;
    }

    scenario.Timeout.ProcessName = Utils.ReplaceCustomVars(scenario.Timeout.ProcessName ?? string.Empty);
    
    if (string.IsNullOrEmpty(scenario.Timeout.ProcessArguments))
    {
        scenario.Timeout.ProcessArguments = configuration.Timeout.ProcessArguments;
    }

    scenario.Timeout.ProcessArguments = Utils.ReplaceCustomVars(scenario.Timeout.ProcessArguments ?? string.Empty);
}

static async Task<ScenarioResult> ProcessScenarioAsync(Scenario scenario, Config configuration)
{
    Stopwatch? watch = null;
    AnsiConsole.MarkupLine("[blue1]Scenario:[/] {0}", scenario.Name);
    AnsiConsole.Markup("  [gold3_1]Warming up[/]");
    watch = Stopwatch.StartNew();
    await RunScenarioAsync(configuration.WarmUpCount, scenario).ConfigureAwait(false);
    AnsiConsole.MarkupLine("    Duration: {0}s", watch.Elapsed.TotalSeconds);
    AnsiConsole.Markup("  [green3]Run[/]");
    var start = DateTime.UtcNow;
    watch = Stopwatch.StartNew();
    var dataPoints = await RunScenarioAsync(configuration.Count, scenario).ConfigureAwait(false);
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
        Count = configuration.Count,
        WarmUpCount = configuration.WarmUpCount,
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

static async Task<List<DataPoint>> RunScenarioAsync(int count, Scenario scenario)
{
    var dataPoints = new List<DataPoint>();
    AnsiConsole.Markup(" ");
    for (var i = 0; i < count; i++)
    {
        var currentRun = await RunProcessCmdAsync(scenario).ConfigureAwait(false);
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

static async Task<DataPoint> RunProcessCmdAsync(Scenario scenario)
{
    // Prepare variables
    var cmdString = scenario.ProcessName ?? string.Empty;
    var cmdArguments = scenario.ProcessArguments ?? string.Empty;
    var workingDirectory = scenario.WorkingDirectory ?? string.Empty;
    var cmdTimeout = scenario.Timeout?.MaxDuration ?? 0;
    var timeoutCmdString = scenario.Timeout?.ProcessName ?? string.Empty;
    var timeoutCmdArguments = scenario.Timeout?.ProcessArguments ?? string.Empty;

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

    // Setup the command
    var cmd = Cli.Wrap(cmdString)
        .WithEnvironmentVariables(cmdEnvironmentVariables)
        .WithWorkingDirectory(workingDirectory);
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
            _ = RunTimeoutProcessCmdAsync(TimeSpan.FromSeconds(cmdTimeout), timeoutCmdString, timeoutCmdArguments,
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

    return dataPoint;
}

static async Task RunTimeoutProcessCmdAsync(TimeSpan timeout, string timeoutCmd, string timeoutArgument,
    string workingDirectory, int targetPid, Action targetCancellation, CancellationToken cancellationToken)
{
    try
    {
        await Task.Delay(timeout, cancellationToken).ConfigureAwait(false);
        if (!cancellationToken.IsCancellationRequested)
        {
            timeoutCmd = timeoutCmd.Replace("%pid%", targetPid.ToString());
            timeoutArgument = timeoutArgument.Replace("%pid%", targetPid.ToString());
            var cmd = Cli.Wrap(timeoutCmd)
                .WithWorkingDirectory(workingDirectory);
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

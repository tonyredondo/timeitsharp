using System.Globalization;
using System.Reflection;
using DatadogTestLogger.Vendors.Datadog.Trace;
using DatadogTestLogger.Vendors.Datadog.Trace.Ci;
using DatadogTestLogger.Vendors.Datadog.Trace.Ci.Logging.DirectSubmission;
using Spectre.Console;
using TimeItSharp.Common.Results;
using Status = TimeItSharp.Common.Results.Status;

namespace TimeItSharp.Common.Exporters;

public sealed class DatadogExporter : IExporter
{
    private string? _configName;
    private InitOptions _options;
    private readonly TestSession _testSession;
    private readonly DateTime _startDate;
    private TestModule? _testModule;
    private FieldInfo? _scopeField = typeof(Test).GetField("_scope", BindingFlags.Instance | BindingFlags.NonPublic);

    public DatadogExporter()
    {
        Environment.SetEnvironmentVariable("DD_CIVISIBILITY_LOGS_ENABLED", "true");
        _testSession = TestSession.GetOrCreate(Environment.CommandLine, Environment.CurrentDirectory, "time-it");
        _startDate = DateTime.UtcNow;
    }
    
    /// <inheritdoc />
    public string Name => "Datadog";

    /// <inheritdoc />
    public bool Enabled => _options.Configuration?.EnableDatadog ?? true;

    /// <inheritdoc />
    public void Initialize(InitOptions options)
    {
        _options = options;
        _configName = options.Configuration?.Name;
        if (string.IsNullOrEmpty(_configName))
        {
            _configName = options.Configuration?.FileName;
        }

        _testModule ??= _testSession.CreateModule(_configName ?? "config_file", "time-it", typeof(DatadogExporter).Assembly.GetName().Version?.ToString() ?? "(unknown)", _startDate);
    }

    /// <inheritdoc />
    public void Export(TimeitResult results)
    {
        var errors = false;
        var minStartDate = results.Scenarios.Select(r => r.Start).Min();
        _testModule ??= _testSession.CreateModule(_configName ?? "config_file", "time-it", typeof(DatadogExporter).Assembly.GetName().Version?.ToString() ?? "(unknown)", minStartDate);
        var testSuite = _testModule.GetOrCreateSuite(_configName is not null ? $"{_configName}.scenarios" : "scenarios", minStartDate);
        try
        {
            for (var i = 0; i < results.Scenarios.Count; i++)
            {
                var scenarioResult = results.Scenarios[i];
                var test = testSuite.CreateTest(scenarioResult.Name, scenarioResult.Start);

                // Set benchmark metadata
                test.SetBenchmarkMetadata(new BenchmarkHostInfo
                {
                    OsVersion = Environment.OSVersion.VersionString,
                    ProcessorCount = Environment.ProcessorCount,
                    RuntimeVersion = FrameworkDescription.Instance.ProductVersion,
                }, new BenchmarkJobInfo
                {
                    Platform = FrameworkDescription.Instance.OSPlatform,
                    RuntimeName = FrameworkDescription.Instance.Name,
                });

                // Add duration benchmark data
                test.AddBenchmarkData(
                    BenchmarkMeasureType.Duration,
                    "Duration of a run",
                    BenchmarkDiscreteStats.GetFrom(scenarioResult.Durations.ToArray()));

                // Report benchmark duration data
                test.SetTag("benchmark.duration.bimodal", scenarioResult.IsBimodal ? "true": "false");
                test.SetTag("benchmark.duration.peakcount", scenarioResult.PeakCount);
                test.SetTag("benchmark.duration.outliers_threshold", Math.Round(scenarioResult.OutliersThreshold, 2));
                test.SetTag("benchmark.duration.outliers_count", scenarioResult.Outliers?.Count ?? 0);

                // Add metrics
                if (scenarioResult.MetricsData.TryGetValue("process.time_to_start_ms", out var timeToStart))
                {
                    var timeToStartArray = timeToStart.Select(v => v * 1000000).ToArray();
                    test.AddBenchmarkData(
                        BenchmarkMeasureType.ApplicationLaunch,
                        "Time expend in application startup",
                        BenchmarkDiscreteStats.GetFrom(timeToStartArray));
                }

                if (scenarioResult.MetricsData.TryGetValue("process.internal_duration_ms", out var internalDuration))
                {
                    var internalDurationArray = internalDuration.Select(v => v * 1000000).ToArray();
                    test.AddBenchmarkData(
                        BenchmarkMeasureType.RunTime,
                        "Time expend in application run",
                        BenchmarkDiscreteStats.GetFrom(internalDurationArray));
                }

                foreach (var metric in scenarioResult.Metrics)
                {
                    // Due to a backend limitation on big objects we only store metrics ending in
                    // .n, .mean, .max, .min and .std_dev
                    if (metric.Key.EndsWith(".n") ||
                        metric.Key.EndsWith(".mean") ||
                        metric.Key.EndsWith(".median") ||
                        metric.Key.EndsWith(".max") ||
                        metric.Key.EndsWith(".min") ||
                        metric.Key.EndsWith(".std_dev"))
                    {
                        test.SetTag($"metrics.{metric.Key}", metric.Value);
                    }
                }

                // Add custom metrics
                foreach (var metric in scenarioResult.AdditionalMetrics)
                {
                    test.SetTag($"metrics.{metric.Key}", metric.Value);
                }

                // Set Error
                if (!string.IsNullOrEmpty(scenarioResult.Error))
                {
                    test.SetErrorInfo("Time-It Error", scenarioResult.Error, null);
                }

                // Meta configuration
                test.SetTag("test.configuration.process_name", scenarioResult.ProcessName);
                test.SetTag("test.configuration.process_arguments", scenarioResult.ProcessArguments);
                test.SetTag("test.working_directory", scenarioResult.WorkingDirectory);
                foreach (var envVar in scenarioResult.EnvironmentVariables)
                {
                    test.SetTag($"test.environment_variables.{envVar.Key}", envVar.Value);
                }

                // Setting custom tags
                foreach (var tag in scenarioResult.Tags)
                {
                    var key = _options.TemplateVariables.Expand(tag.Key);
                    if (tag.Value is string strValue)
                    {
                        test.SetTag(key, _options.TemplateVariables.Expand(strValue));
                    }
                    else if (tag.Value is IConvertible convertible)
                    {
                        test.SetTag(key, convertible.ToDouble(CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        test.SetTag(key, tag.Value?.ToString());
                    }
                }
                
                // Add overheads
                if (results.Overheads is not null)
                {
                    for (var j = 0; j < results.Overheads.Length; j++)
                    {
                        if (i == j)
                        {
                            continue;
                        }

                        var overheads = results.Overheads[j];
                        var name = results.Scenarios[j].Name;
                        test.SetTag($"test.overhead_over.{name}", Math.Round(overheads[i], 2));
                    }
                }

                // Add log messages
                if (!string.IsNullOrEmpty(scenarioResult.LastStandardOutput) && _scopeField?.GetValue(test) is Scope scope)
                {
                    foreach (var line in scenarioResult.LastStandardOutput.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries))
                    {
                        Tracer.Instance.TracerManager.DirectLogSubmission.Sink.EnqueueLog(
                            new CIVisibilityLogEvent("xunit", "info", line, scope.Span));
                    }
                }
                
                // Close test
                test.Close(scenarioResult.Status == Status.Passed ? TestStatus.Pass : TestStatus.Fail,
                    scenarioResult.Duration);
            }
        }
        catch (Exception ex)
        {
            errors = true;
            AnsiConsole.MarkupLine("[red]Error exporting to datadog:[/]");
            AnsiConsole.WriteException(ex);
        }
        finally
        {
            testSuite.Close();
            _testModule.Close();
            _testSession.Close(TestStatus.Pass);
        }

        if (!errors)
        {
            AnsiConsole.MarkupLine($"[lime]The Datadog exported ran successfully.[/]");
        }
    }
}
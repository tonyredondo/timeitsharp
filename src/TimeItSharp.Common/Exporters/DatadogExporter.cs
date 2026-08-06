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
    private TestSession? _testSession;
    private readonly DateTime _startDate;
    private TestModule? _testModule;
    private FieldInfo? _scopeField = typeof(Test).GetField("_scope", BindingFlags.Instance | BindingFlags.NonPublic);

    public DatadogExporter()
    {
        // The target processes are launched before exporters are initialized. Set this process-wide
        // switch while the exporter instance is created so child processes inherit it.
        Environment.SetEnvironmentVariable("DD_CIVISIBILITY_LOGS_ENABLED", "true");
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
        if (!Enabled)
        {
            return;
        }

        _testSession ??= TestSession.InternalGetOrCreate(Environment.CommandLine, Environment.CurrentDirectory, "time-it");
        _configName = options.Configuration?.Name;
        if (string.IsNullOrEmpty(_configName))
        {
            _configName = options.Configuration?.FileName;
        }

        _testModule ??= _testSession.InternalCreateModule(_configName ?? "config_file", "time-it", typeof(DatadogExporter).Assembly.GetName().Version?.ToString() ?? "(unknown)", _startDate);
    }

    /// <inheritdoc />
    public void Export(TimeitResult results)
    {
        var testSession = _testSession;
        if (!Enabled || testSession is null)
        {
            return;
        }

        if (results.Scenarios.Count == 0)
        {
            try
            {
                _testModule?.Close();
            }
            finally
            {
                testSession.Close(TestStatus.Fail);
            }

            return;
        }

        var errors = false;
        TestSuite? testSuite = null;
        try
        {
            var minStartDate = results.Scenarios.Select(r => r.Start).Min();
            _testModule ??= testSession.InternalCreateModule(_configName ?? "config_file", "time-it", typeof(DatadogExporter).Assembly.GetName().Version?.ToString() ?? "(unknown)", minStartDate);
            testSuite = _testModule.InternalGetOrCreateSuite(_configName is not null ? $"{_configName}.scenarios" : "scenarios", minStartDate);
            for (var i = 0; i < results.Scenarios.Count; i++)
            {
                var scenarioResult = results.Scenarios[i];
                Test? test;
                if (scenarioResult.Scenario is { } scenario)
                {
                    DatadogMetadata.GetIds(scenario, out var traceId, out var spanId);
                    test = testSuite.InternalCreateTest(scenarioResult.Name, scenarioResult.Start, traceId, spanId);
                }
                else
                {
                    test = testSuite.InternalCreateTest(scenarioResult.Name, scenarioResult.Start);
                }

                // Source file
                var filePath = _options.Configuration.FilePath;
                if (!string.IsNullOrEmpty(filePath))
                {
                    filePath = Path.IsPathFullyQualified(filePath) ? filePath : Path.GetFullPath(filePath);
                    string? relativePath = null;
                    try
                    {
                        relativePath = CIEnvironmentValues.Instance.MakeRelativePathFromSourceRoot(filePath, false);
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine("[red]Error exporting to datadog:[/]");
                        AnsiConsole.WriteException(ex);
                    }

                    if (relativePath is not null)
                    {
                        test.SetTag("test.source.file", relativePath);
                        if (CIEnvironmentValues.Instance.CodeOwners is { } codeOwners &&
                            codeOwners.Match("/" + relativePath) is { } entry)
                        {
                            test.SetTag("test.codeowners", entry.GetOwnersString());
                        }
                    }
                }

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

                // Keep the benchmark measure present even when the sample set is empty. The
                // Datadog stats type represents an empty sample set safely, and omitting the
                // measure changes the exported span schema.
                test.AddBenchmarkData(
                    BenchmarkMeasureType.Duration,
                    "Duration of a run",
                    BenchmarkDiscreteStats.GetFrom(scenarioResult.Durations.Where(double.IsFinite).ToArray()));

                // Report benchmark duration data
                test.SetTag("benchmark.duration.bimodal", scenarioResult.IsBimodal ? "true": "false");
                test.SetTag("benchmark.duration.peakcount", scenarioResult.PeakCount);
                test.SetTag("benchmark.duration.outliers_threshold", Math.Round(FiniteOrZero(scenarioResult.OutliersThreshold), 2));
                test.SetTag("benchmark.duration.outliers_count", scenarioResult.Outliers?.Count ?? 0);

                // Add metrics
                if (scenarioResult.MetricsData.TryGetValue("process.time_to_start_ms", out var timeToStart))
                {
                    var timeToStartArray = timeToStart
                        .Select(v => v * 1000000)
                        .Where(double.IsFinite)
                        .ToArray();
                    test.AddBenchmarkData(
                        BenchmarkMeasureType.ApplicationLaunch,
                        "Time expend in application startup",
                        BenchmarkDiscreteStats.GetFrom(timeToStartArray));
                }

                if (scenarioResult.MetricsData.TryGetValue("process.internal_duration_ms", out var internalDuration))
                {
                    var internalDurationArray = internalDuration
                        .Select(v => v * 1000000)
                        .Where(double.IsFinite)
                        .ToArray();
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
                        if (double.IsFinite(metric.Value))
                        {
                            test.SetTag($"metrics.{metric.Key}", metric.Value);
                        }
                    }
                }

                // Add custom metrics
                foreach (var metric in scenarioResult.AdditionalMetrics)
                {
                    if (double.IsFinite(metric.Value))
                    {
                        test.SetTag($"metrics.{metric.Key}", metric.Value);
                    }
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
                    if (Utils.IsSensitiveEnvironmentVariable(envVar.Key))
                    {
                        continue;
                    }

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
                        test.SetTag(key, _options.TemplateVariables.Expand(tag.Value?.ToString() ?? string.Empty));
                    }
                }
                
                // Add overheads
                if (results.Overheads is not null)
                {
                    var overheadCount = Math.Min(results.Overheads.Length, results.Scenarios.Count);
                    for (var j = 0; j < overheadCount; j++)
                    {
                        if (i == j || i >= results.Overheads[j].Length)
                        {
                            continue;
                        }

                        var overhead = results.Overheads[j][i];
                        var name = results.Scenarios[j].Name;
                        test.SetTag($"test.overhead_over.{name}", Math.Round(FiniteOrZero(overhead.OverheadPercentage), 2));
                        test.SetTag($"test.overhead_over.{name}.delta", Math.Round(FiniteOrZero(overhead.DeltaValue), 2));
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
                test.SetTag("test.final_status", scenarioResult.Status == Status.Passed ? "pass" : "fail");
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
            testSuite?.Close();
            _testModule?.Close();
            var sessionStatus = errors || results.Scenarios.Any(s => s.Status == Status.Failed)
                ? TestStatus.Fail
                : TestStatus.Pass;
            testSession.Close(sessionStatus);
        }

        if (!errors && results.Scenarios.All(s => s.Status == Status.Passed))
        {
            AnsiConsole.MarkupLine("[lime]The Datadog export ran successfully.[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]The Datadog export completed with errors.[/]");
        }
    }

    private static double FiniteOrZero(double value) => double.IsFinite(value) ? value : 0;

}

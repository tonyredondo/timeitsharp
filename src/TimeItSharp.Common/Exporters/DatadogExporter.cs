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
    private readonly FieldInfo? _scopeField = typeof(Test).GetField(
        "_scope", BindingFlags.Instance | BindingFlags.NonPublic);
    private bool _sessionClosed;

    public DatadogExporter()
    {
        // Do not mutate DD_CIVISIBILITY_LOGS_ENABLED here.  Exporters are initialized after the
        // target process has already been launched, and changing this process-wide variable leaks
        // state into subsequent runs.  The service/launcher owns child-process environment setup.
        _startDate = DateTime.UtcNow;
    }

    public string Name => "Datadog";

    public bool Enabled => _options.Configuration?.EnableDatadog ?? false;

    public void Initialize(InitOptions options)
    {
        _options = options;
        if (!Enabled || _sessionClosed)
        {
            return;
        }

        var configuration = _options.Configuration;
        var templateSecrets = Utils.GetSensitiveEnvironmentValues(configuration?.EnvironmentVariables)
            .Concat(Utils.GetTemplateSecretValues(_options.TemplateVariables))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        _configName = Utils.SanitizeText(configuration?.Name, templateSecrets);
        if (string.IsNullOrEmpty(_configName))
        {
            _configName = Utils.SanitizeText(configuration?.FileName, templateSecrets);
        }

        try
        {
            _testSession ??= TestSession.InternalGetOrCreate(
                Utils.SanitizeText(Environment.CommandLine, templateSecrets),
                Utils.SanitizeText(Environment.CurrentDirectory, templateSecrets),
                "time-it");
            _testModule ??= _testSession.InternalCreateModule(
                string.IsNullOrEmpty(_configName) ? "config_file" : _configName,
                "time-it",
                typeof(DatadogExporter).Assembly.GetName().Version?.ToString() ?? "(unknown)",
                _startDate);
        }
        catch (Exception ex)
        {
            ReportException(ex, templateSecrets);
            // Initialization can fail after creating a session or module. Close partial
            // resources here because the engine deliberately skips Export for failed instances.
            try
            {
                _testModule?.Close();
            }
            catch (Exception closeError)
            {
                ReportException(closeError, templateSecrets);
            }

            try
            {
                _testSession?.Close(TestStatus.Fail);
            }
            catch (Exception closeError)
            {
                ReportException(closeError, templateSecrets);
            }

            _testModule = null;
            _testSession = null;
            _sessionClosed = true;
            throw Utils.SanitizeException(ex, templateSecrets);
        }
    }

    public void Export(TimeitResult results)
    {
        if (!Enabled || _sessionClosed)
        {
            return;
        }

        // Be defensive for callers outside TimeItEngine: Initialize is normally called first, but
        // direct exporter use should not expose a null session or a partially initialized graph.
        if (_testSession is null)
        {
            Initialize(_options);
        }

        var testSession = _testSession;
        if (testSession is null)
        {
            return;
        }

        var safeResults = Utils.SanitizeTimeitResult(results, _options.TemplateVariables,
                Utils.GetSensitiveEnvironmentValues(_options.Configuration?.EnvironmentVariables));
        var safeScenarios = safeResults.Scenarios ?? Array.Empty<ScenarioResult>();
        var originalScenarios = results?.Scenarios?.Where(item => item is not null).ToList()
                                ?? new List<ScenarioResult>();
        var exportErrors = new List<Exception>();
        TestSuite? testSuite = null;

        try
        {
            if (safeScenarios.Count > 0)
            {
                var minStartDate = safeScenarios.Min(item => item.Start);
                _testModule ??= testSession.InternalCreateModule(
                    string.IsNullOrEmpty(_configName) ? "config_file" : _configName,
                    "time-it",
                    typeof(DatadogExporter).Assembly.GetName().Version?.ToString() ?? "(unknown)",
                    minStartDate);
                testSuite = _testModule.InternalGetOrCreateSuite(
                    string.IsNullOrEmpty(_configName) ? "scenarios" : $"{_configName}.scenarios",
                    minStartDate);

                for (var i = 0; i < safeScenarios.Count; i++)
                {
                    var scenarioResult = safeScenarios[i];
                    var knownSecrets = Utils.GetSecretValues(
                            i < originalScenarios.Count ? originalScenarios[i] : scenarioResult)
                        .Concat(Utils.GetSensitiveEnvironmentValues(
                            _options.Configuration?.EnvironmentVariables))
                        .Concat(Utils.GetTemplateSecretValues(_options.TemplateVariables))
                        .Distinct(StringComparer.Ordinal)
                        .ToArray();
                    Test? test = null;
                    var scenarioFailed = scenarioResult.Status != Status.Passed;
                    try
                    {
                        // Keep the trace/span association from the runtime Scenario object without
                        // copying that object graph into the sanitized result.
                        if (i < originalScenarios.Count && originalScenarios[i]?.Scenario is { } scenario)
                        {
                            DatadogMetadata.GetIds(scenario, out var traceId, out var spanId);
                            test = testSuite.InternalCreateTest(
                                Utils.SanitizeText(scenarioResult.Name, knownSecrets),
                                scenarioResult.Start,
                                traceId,
                                spanId);
                        }
                        else
                        {
                            test = testSuite.InternalCreateTest(
                                Utils.SanitizeText(scenarioResult.Name, knownSecrets),
                                scenarioResult.Start);
                        }

                        ExportScenario(test, scenarioResult, safeResults, i, knownSecrets);
                    }
                    catch (Exception ex)
                    {
                        scenarioFailed = true;
                        exportErrors.Add(Utils.SanitizeException(ex, knownSecrets));
                        ReportException(ex, knownSecrets);
                        if (test is not null)
                        {
                            try
                            {
                                test.SetErrorInfo("Time-It Error", Utils.SanitizeText(ex.Message, knownSecrets), null);
                            }
                            catch (Exception closeError)
                            {
                                exportErrors.Add(Utils.SanitizeException(closeError, knownSecrets));
                                ReportException(closeError, knownSecrets);
                            }
                        }
                    }
                    finally
                    {
                        if (test is not null)
                        {
                            try
                            {
                                test.SetTag("test.final_status", scenarioFailed ? "fail" : "pass");
                                test.Close(
                                    scenarioFailed ? TestStatus.Fail : TestStatus.Pass,
                                    SafeDuration(scenarioResult.Duration));
                            }
                            catch (Exception ex)
                            {
                                exportErrors.Add(Utils.SanitizeException(ex, knownSecrets));
                                ReportException(ex, knownSecrets);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Module/suite creation can fail before a scenario is entered.  Preserve the same
            // cleanup/status contract as per-scenario failures and continue to close the session.
            exportErrors.Add(Utils.SanitizeException(ex));
            ReportException(ex);
        }
        finally
        {
            TryClose(testSuite, "test suite", exportErrors);
            TryClose(_testModule, "test module", exportErrors);
            var sessionStatus = exportErrors.Count > 0 || safeScenarios.Any(item => item.Status != Status.Passed)
                ? TestStatus.Fail
                : TestStatus.Pass;
            try
            {
                testSession.Close(sessionStatus);
            }
            catch (Exception ex)
            {
                exportErrors.Add(Utils.SanitizeException(ex));
                ReportException(ex);
            }

            _sessionClosed = true;
            _testSession = null;
            _testModule = null;
        }

        if (exportErrors.Count == 0 && safeScenarios.All(item => item.Status == Status.Passed))
        {
            AnsiConsole.MarkupLine("[lime]The Datadog export ran successfully.[/]");
            return;
        }

        AnsiConsole.MarkupLine("[yellow]The Datadog export completed with errors.[/]");
        // The engine treats an exporter exception as a non-zero result.  Do not hide failures
        // behind a successful return merely because the backend accepted some scenarios.
        if (exportErrors.Count > 0)
        {
            throw new AggregateException("One or more Datadog scenarios could not be exported.", exportErrors);
        }
    }

    private void ExportScenario(
        Test test,
        ScenarioResult scenarioResult,
        TimeitResult results,
        int scenarioIndex,
        IReadOnlyList<string> knownSecrets)
    {
        var configuration = _options.Configuration;
        var sourceFile = configuration?.FilePath;
        if (!string.IsNullOrEmpty(sourceFile))
        {
            sourceFile = Utils.SanitizeText(sourceFile, knownSecrets);
            sourceFile = Path.IsPathFullyQualified(sourceFile) ? sourceFile : Path.GetFullPath(sourceFile);
            string? relativePath = null;
            try
            {
                relativePath = CIEnvironmentValues.Instance.MakeRelativePathFromSourceRoot(sourceFile, false);
            }
            catch (Exception ex)
            {
                ReportException(ex, knownSecrets);
            }

            if (!string.IsNullOrEmpty(relativePath))
            {
                relativePath = Utils.SanitizeText(relativePath, knownSecrets);
                test.SetTag("test.source.file", relativePath);
                if (CIEnvironmentValues.Instance.CodeOwners is { } codeOwners &&
                    codeOwners.Match("/" + relativePath) is { } entry)
                {
                    test.SetTag("test.codeowners", Utils.SanitizeText(entry.GetOwnersString(), knownSecrets));
                }
            }
        }

        test.SetBenchmarkMetadata(
            new BenchmarkHostInfo
            {
                OsVersion = Utils.SanitizeText(Environment.OSVersion.VersionString, knownSecrets),
                ProcessorCount = Environment.ProcessorCount,
                RuntimeVersion = Utils.SanitizeText(FrameworkDescription.Instance.ProductVersion, knownSecrets),
            },
            new BenchmarkJobInfo
            {
                Platform = Utils.SanitizeText(FrameworkDescription.Instance.OSPlatform, knownSecrets),
                RuntimeName = Utils.SanitizeText(FrameworkDescription.Instance.Name, knownSecrets),
            });

        // Keep the benchmark measure/tag contract even for an empty failed run. The Datadog
        // stats type accepts an empty sample set; omitting the measure would change the span
        // schema. Non-finite samples are discarded before entering the stats object.
        var durationValues = scenarioResult.Durations.Where(double.IsFinite).ToArray();
        test.AddBenchmarkData(
            BenchmarkMeasureType.Duration,
            "Duration of a run",
            BenchmarkDiscreteStats.GetFrom(durationValues));

        test.SetTag("benchmark.duration.bimodal", scenarioResult.IsBimodal ? "true" : "false");
        test.SetTag("benchmark.duration.peakcount", scenarioResult.PeakCount);
        test.SetTag("benchmark.duration.outliers_threshold", Math.Round(FiniteOrZero(scenarioResult.OutliersThreshold), 2));
        test.SetTag("benchmark.duration.outliers_count", scenarioResult.Outliers?.Count ?? 0);

        if (scenarioResult.MetricsData.TryGetValue("process.time_to_start_ms", out var timeToStart))
        {
            var startupValues = timeToStart.Where(double.IsFinite)
                .Select(v => v * 1_000_000)
                .Where(double.IsFinite)
                .ToArray();
            test.AddBenchmarkData(
                BenchmarkMeasureType.ApplicationLaunch,
                "Time expend in application startup",
                BenchmarkDiscreteStats.GetFrom(startupValues));
        }

        if (scenarioResult.MetricsData.TryGetValue("process.internal_duration_ms", out var internalDuration))
        {
            var runValues = internalDuration.Where(double.IsFinite)
                .Select(v => v * 1_000_000)
                .Where(double.IsFinite)
                .ToArray();
            test.AddBenchmarkData(
                BenchmarkMeasureType.RunTime,
                "Time expend in application run",
                BenchmarkDiscreteStats.GetFrom(runValues));
        }

        foreach (var metric in scenarioResult.Metrics)
        {
            if (Utils.IsSensitiveEnvironmentVariable(metric.Key) || !double.IsFinite(metric.Value))
            {
                continue;
            }

            if (metric.Key.EndsWith(".n", StringComparison.Ordinal) ||
                metric.Key.EndsWith(".mean", StringComparison.Ordinal) ||
                metric.Key.EndsWith(".median", StringComparison.Ordinal) ||
                metric.Key.EndsWith(".max", StringComparison.Ordinal) ||
                metric.Key.EndsWith(".min", StringComparison.Ordinal) ||
                metric.Key.EndsWith(".std_dev", StringComparison.Ordinal))
            {
                test.SetTag($"metrics.{Utils.SanitizeText(metric.Key, knownSecrets)}", metric.Value);
            }
        }

        foreach (var metric in scenarioResult.AdditionalMetrics)
        {
            if (!double.IsFinite(metric.Value) || Utils.IsSensitiveEnvironmentVariable(metric.Key))
            {
                continue;
            }

            var key = Utils.SanitizeText(metric.Key, knownSecrets);
            if (!string.IsNullOrWhiteSpace(key))
            {
                test.SetTag($"metrics.{key}", metric.Value);
            }
        }

        if (!string.IsNullOrEmpty(scenarioResult.Error))
        {
            test.SetErrorInfo("Time-It Error", Utils.SanitizeText(scenarioResult.Error, knownSecrets), null);
        }

        test.SetTag("test.configuration.process_name", Utils.SanitizeText(scenarioResult.ProcessName, knownSecrets));
        test.SetTag("test.configuration.process_arguments", Utils.SanitizeText(scenarioResult.ProcessArguments, knownSecrets));
        test.SetTag("test.working_directory", Utils.SanitizeText(scenarioResult.WorkingDirectory, knownSecrets));
        foreach (var envVar in scenarioResult.EnvironmentVariables)
        {
            if (string.IsNullOrWhiteSpace(envVar.Key))
            {
                continue;
            }

            var key = Utils.SanitizeText(envVar.Key, knownSecrets);
            var value = Utils.IsSensitiveEnvironmentVariable(envVar.Key)
                ? Utils.RedactedValue
                : Utils.SanitizeText(envVar.Value, knownSecrets);
            test.SetTag($"test.environment_variables.{key}", value);
        }

        foreach (var tag in scenarioResult.Tags)
        {
            var key = _options.TemplateVariables is { } variables
                ? variables.Expand(tag.Key)
                : tag.Key;
            key = Utils.SanitizeText(key, knownSecrets);
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var value = Utils.ToDatadogTagValue(tag.Value, key, knownSecrets);
            if (value is not null)
            {
                SetTagValue(test, key, value, knownSecrets);
            }
        }

        if (results.Overheads is not null)
        {
            var overheadCount = Math.Min(results.Overheads.Length, results.Scenarios.Count);
            for (var j = 0; j < overheadCount; j++)
            {
                if (scenarioIndex == j || results.Overheads[j] is null || scenarioIndex >= results.Overheads[j].Length)
                {
                    continue;
                }

                var overhead = results.Overheads[j][scenarioIndex];
                var name = j < results.Scenarios.Count
                    ? Utils.SanitizeText(results.Scenarios[j].Name, knownSecrets)
                    : string.Empty;
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                test.SetTag($"test.overhead_over.{name}", Math.Round(FiniteOrZero(overhead.OverheadPercentage), 2));
                test.SetTag($"test.overhead_over.{name}.delta", Math.Round(FiniteOrZero(overhead.DeltaValue), 2));
            }
        }

        if (_scopeField?.GetValue(test) is Scope scope)
        {
            var output = Utils.SanitizeOutput(scenarioResult.LastStandardOutput, knownSecrets);
            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                Tracer.Instance.TracerManager.DirectLogSubmission.Sink.EnqueueLog(
                    new CIVisibilityLogEvent("xunit", "info", line, scope.Span));
            }
        }
    }


    private static void SetTagValue(Test test, string key, object value, IEnumerable<string> knownSecrets)
    {
        switch (value)
        {
            case string stringValue:
                test.SetTag(key, stringValue);
                break;
            case bool boolValue:
                test.SetTag(key, boolValue ? "true" : "false");
                break;
            case double doubleValue when double.IsFinite(doubleValue):
                test.SetTag(key, doubleValue);
                break;
            case float floatValue when float.IsFinite(floatValue):
                test.SetTag(key, (double)floatValue);
                break;
            case decimal decimalValue:
                test.SetTag(key, (double)decimalValue);
                break;
            case IConvertible convertible:
                try
                {
                    var number = convertible.ToDouble(CultureInfo.InvariantCulture);
                    if (double.IsFinite(number))
                    {
                        test.SetTag(key, number);
                        break;
                    }
                }
                catch (Exception ex)
                {
                    ReportException(ex, knownSecrets);
                }

                test.SetTag(key, Utils.SanitizeText(value.ToString(), knownSecrets));
                break;
            default:
                test.SetTag(key, Utils.SanitizeText(value.ToString(), knownSecrets));
                break;
        }
    }

    private static void TryClose(object? resource, string resourceName, ICollection<Exception> errors)
    {
        if (resource is null)
        {
            return;
        }

        try
        {
            switch (resource)
            {
                case TestSuite suite:
                    suite.Close();
                    break;
                case TestModule module:
                    module.Close();
                    break;
            }
        }
        catch (Exception ex)
        {
            errors.Add(Utils.SanitizeException(ex));
            ReportException(ex);
        }
    }

    private static TimeSpan SafeDuration(TimeSpan duration)
    {
        return duration < TimeSpan.Zero ? TimeSpan.Zero : duration;
    }

    private static double FiniteOrZero(double value) => double.IsFinite(value) ? value : 0;

    private static void ReportException(Exception exception, IEnumerable<string>? knownSecrets = null)
    {
        AnsiConsole.MarkupLine("[red]Error exporting to datadog:[/]");
        AnsiConsole.WriteException(Utils.SanitizeException(exception, knownSecrets));
    }
}

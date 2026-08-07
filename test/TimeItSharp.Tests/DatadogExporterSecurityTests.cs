using System.Reflection;
using System.Collections;
using TimeItSharp.Common.Configuration;
using TimeItSharp.Common.Exporters;
using TimeItSharp.Common.Results;

namespace TimeItSharp.Tests;

[Collection("Datadog exporter session")]
public sealed class DatadogExporterSecurityTests
{
    private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Type TestSessionType =
        Assembly.Load("Datadog.testlogger").GetType(
            "DatadogTestLogger.Vendors.Datadog.Trace.Ci.TestSession", throwOnError: true)!;

    [Fact]
    public void Export_uses_the_detached_scenario_snapshot_without_a_second_source_enumeration()
    {
        var source = new SingleEnumerationScenarios(new ScenarioResult
        {
            Name = "scenario",
            Start = DateTime.UtcNow,
            Duration = TimeSpan.FromMilliseconds(1),
            Durations = [1],
        });
        var ambient = CreateAmbientSession();
        var exporter = new DatadogExporter();
        try
        {
            exporter.Initialize(new InitOptions(
                new Config { EnableDatadog = true, Name = "datadog-security-test" },
                null,
                new TemplateVariables(),
                null));

            var exception = Record.Exception(() =>
                exporter.Export(new TimeitResult { Scenarios = source }));

            Assert.Null(exception);
            Assert.Equal(1, source.EnumerationCount);
        }
        finally
        {
            exporter.Dispose();
            CloseSession(ambient);
        }
    }

    [Fact]
    public void Sanitization_observer_captures_Datadog_ids_during_the_only_source_enumeration()
    {
        var runtimeScenario = new Scenario { Name = "runtime" };
        var expected = GetCorrelation(runtimeScenario);
        var source = new SingleEnumerationScenarios(new ScenarioResult
        {
            Name = "scenario",
            Scenario = runtimeScenario,
        });
        (object TraceId, ulong SpanId)? observed = null;

        Utils.SanitizeTimeitResult(
            new TimeitResult { Scenarios = source },
            scenarioObserver: item => observed = GetCorrelation(item.Scenario!));

        Assert.True(observed.HasValue);
        Assert.Equal(expected.TraceId, observed.Value.TraceId);
        Assert.Equal(expected.SpanId, observed.Value.SpanId);
        Assert.Equal(1, source.EnumerationCount);
    }

    [Fact]
    public void Sanitization_observer_failure_is_contained_without_exception_text()
    {
        const string exceptionSecret = "secret-only-in-observer-exception";
        var source = new SingleEnumerationScenarios(new ScenarioResult { Name = "scenario" });

        var exception = Record.Exception(() =>
        {
            var safe = Utils.SanitizeTimeitResult(
                new TimeitResult { Scenarios = source },
                scenarioObserver: _ => throw new InvalidOperationException(exceptionSecret));
            Assert.DoesNotContain(
                exceptionSecret,
                Assert.Single(safe.Scenarios).Error,
                StringComparison.Ordinal);
        });

        Assert.Null(exception);
        Assert.Equal(1, source.EnumerationCount);
    }

    private static (object TraceId, ulong SpanId) GetCorrelation(object scenario)
    {
        var getIds = typeof(DatadogMetadata).GetMethod(
            nameof(DatadogMetadata.GetIds),
            BindingFlags.Static | BindingFlags.Public)!;
        object?[] arguments = [scenario, null, 0UL];
        getIds.Invoke(null, arguments);
        return (arguments[1]!, (ulong)arguments[2]!);
    }

    private static object CreateAmbientSession()
    {
        var getOrCreate = TestSessionType.GetMethod(
            "InternalGetOrCreate",
            StaticMembers,
            binder: null,
            types: [typeof(string), typeof(string), typeof(string)],
            modifiers: null)!;
        return getOrCreate.Invoke(null, ["ambient-security-session", Environment.CurrentDirectory, "xunit"])!;
    }

    private static void CloseSession(object session)
    {
        var close = TestSessionType.GetMethods(InstanceMembers)
            .Single(method => method.Name == "Close" &&
                              method.GetParameters() is [{ ParameterType.IsEnum: true }]);
        var statusType = close.GetParameters()[0].ParameterType;
        close.Invoke(session, [Enum.Parse(statusType, "Fail")]);
    }

    private sealed class SingleEnumerationScenarios(ScenarioResult scenario) :
        IReadOnlyList<ScenarioResult>
    {
        public int EnumerationCount { get; private set; }

        public int Count => 1;

        public ScenarioResult this[int index] => index == 0
            ? scenario
            : throw new ArgumentOutOfRangeException(nameof(index));

        public IEnumerator<ScenarioResult> GetEnumerator()
        {
            EnumerationCount++;
            if (EnumerationCount > 1)
            {
                throw new InvalidOperationException("secret-only-in-second-enumeration-error");
            }

            yield return scenario;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

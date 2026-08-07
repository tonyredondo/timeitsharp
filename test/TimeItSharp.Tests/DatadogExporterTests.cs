using System.Reflection;
using TimeItSharp.Common.Configuration;
using TimeItSharp.Common.Exporters;

namespace TimeItSharp.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DatadogExporterCollection
{
    public const string Name = "Datadog exporter session";
}

[Collection(DatadogExporterCollection.Name)]
public sealed class DatadogExporterTests
{
    private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Type TestSessionType =
        Assembly.Load("Datadog.testlogger").GetType(
            "DatadogTestLogger.Vendors.Datadog.Trace.Ci.TestSession", throwOnError: true)!;
    private static readonly PropertyInfo CurrentSession =
        TestSessionType.GetProperty("Current", StaticMembers)!;

    [Fact]
    public void Dispose_is_idempotent_when_export_is_disabled()
    {
        var exporter = new DatadogExporter();
        exporter.Initialize(new InitOptions(new Config { EnableDatadog = false }, null, new TemplateVariables(), null));

        exporter.Dispose();

        Assert.Null(Record.Exception(exporter.Dispose));
    }

    [Fact]
    public void Dispose_does_not_close_a_borrowed_ambient_session()
    {
        var getOrCreate = TestSessionType.GetMethod(
            "InternalGetOrCreate",
            StaticMembers,
            binder: null,
            types: [typeof(string), typeof(string), typeof(string)],
            modifiers: null)!;
        var ambient = getOrCreate.Invoke(null, ["ambient-session", Environment.CurrentDirectory, "xunit"]);
        Assert.NotNull(ambient);
        var exporter = CreateExporter();
        try
        {
            Assert.Same(ambient, CurrentSession.GetValue(null));
            exporter.Dispose();
            Assert.Same(ambient, CurrentSession.GetValue(null));
            Assert.Null(Record.Exception(exporter.Dispose));
        }
        finally
        {
            exporter.Dispose();
            CloseSession(ambient!);
        }
    }

    private static DatadogExporter CreateExporter()
    {
        var exporter = new DatadogExporter();
        exporter.Initialize(new InitOptions(
            new Config { EnableDatadog = true, Name = "datadog-exporter-test" },
            null,
            new TemplateVariables(),
            null));
        return exporter;
    }

    private static void CloseCurrentSession()
    {
        if (CurrentSession.GetValue(null) is { } session)
        {
            CloseSession(session);
        }
    }

    private static void CloseSession(object session)
    {
        var close = TestSessionType.GetMethods(InstanceMembers)
            .Single(method => method.Name == "Close" && method.GetParameters() is [{ ParameterType.IsEnum: true }]);
        var statusType = close.GetParameters()[0].ParameterType;
        close.Invoke(session, [Enum.Parse(statusType, "Fail")]);
    }
}

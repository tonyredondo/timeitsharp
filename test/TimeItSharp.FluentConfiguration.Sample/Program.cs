
using TimeItSharp.Common;
using TimeItSharp.Common.Assertors;
using TimeItSharp.Common.Configuration.Builder;
using TimeItSharp.Common.Exporters;
using TimeItSharp.Common.Services;

var config = ConfigBuilder.Create()
    .WithWarmupCount(1)
    .WithCount(10)
    .WithName("dd-tracer-dotnet test")
    .WithMetrics(true)
    .WithMetricsProcessName("dotnet")
    .WithExporters<ConsoleExporter, JsonExporter, DatadogExporter>()
    .WithAssertor<DefaultAssertor>()
    .WithService<DatadogProfilerService>()
    .WithProcessName("dotnet")
    .WithProcessArguments("--version")
    .WithEnvironmentVariables(new Dictionary<string, string>
    {
        ["CORECLR_ENABLE_PROFILING"] = "1",
        ["CORECLR_PROFILER"] = "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}",
        ["DD_DOTNET_TRACER_HOME"] = "/",
    })
    .WithTags(new Dictionary<string, string>
    {
        ["runtime.architecture"] = "x64",
        ["runtime.name"] = ".NET Framework",
        ["runtime.version"] = "4.6.1",
        ["benchmark.job.runtime.name"] = ".NET Framework 4.6.1",
        ["benchmark.job.runtime.moniker"] = "net461",
    })
    .WithTags("MyMetricValue", 25432.43)
    .WithTimeout(timeout => timeout
        .WithMaxDuration(15)
        .WithProcessName("dotnet-dump")
        .WithProcessArguments("collect --process-id %pid%"))
    .WithScenario(scenario => scenario
        .WithName("Callsite")
        .WithEnvironmentVariable("DD_TRACE_CALLTARGET_ENABLED", "false")
        .WithEnvironmentVariable("DD_CLR_ENABLE_INLINING", "false"))
    .WithScenario(scenario => scenario
        .WithName("CallTarget")
        .WithEnvironmentVariable("DD_TRACE_CALLTARGET_ENABLED", "true")
        .WithEnvironmentVariable("DD_CLR_ENABLE_INLINING", "false"))
    .WithScenario(scenario => scenario
        .WithName("CallTarget & Inlining")
        .WithEnvironmentVariable("DD_TRACE_CALLTARGET_ENABLED", "true")
        .WithEnvironmentVariable("DD_CLR_ENABLE_INLINING", "true"));

Environment.ExitCode = await TimeItEngine.RunAsync(
    configBuilder: config,
    options: new TimeItOptions()
        .AddAssertorState<DefaultAssertor>(50)
        .AddExporterState<ConsoleExporter, JsonExporter, DatadogExporter>(51)
        .AddServiceState<NoopService>(52)
        .AddServiceState<DatadogProfilerService>(
            new DatadogProfilerConfiguration()
                .WithExtraRun(5))
    );

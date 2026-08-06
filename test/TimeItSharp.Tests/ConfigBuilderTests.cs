using TimeItSharp.Common.Assertors;
using TimeItSharp.Common.Configuration;
using TimeItSharp.Common.Configuration.Builder;
using TimeItSharp.Common.Exporters;
using TimeItSharp.Common.Services;

namespace TimeItSharp.Tests;

public sealed class ConfigBuilderTests
{
    [Fact]
    public void Build_populates_process_scenario_timeout_and_extension_settings()
    {
        var config = ConfigBuilder.Create()
            .WithName("sample")
            .WithWarmupCount(2)
            .WithCount(10)
            .WithMetrics(false)
            .WithMetricsProcessName("dotnet")
            .WithMetricsFrequency(500)
            .WithProcessName("dotnet")
            .WithProcessArguments("--version")
            .WithWorkingDirectory("/tmp")
            .WithEnvironmentVariable("ONE", "1")
            .WithEnvironmentVariables(new Dictionary<string, string> { ["TWO"] = "2" })
            .WithPathValidations("first.txt", "second.txt")
            .WithTags("stringTag", "value")
            .WithTags("numericTag", 42)
            .WithTimeout(timeout => timeout
                .WithMaxDuration(15)
                .WithProcessName("dotnet-dump")
                .WithProcessArguments("collect"))
            .WithScenario(scenario => scenario
                .WithName("baseline")
                .AsBaseline()
                .WithEnvironmentVariable("SCENARIO", "baseline")
                .WithTags("scenarioTag", "scenarioValue"))
            .WithAssertor<DefaultAssertor>()
            .WithExporter<JsonExporter>()
            .WithService<NoopService>()
            .Build();

        Assert.Equal("sample", config.Name);
        Assert.Equal(2, config.WarmUpCount);
        Assert.Equal(10, config.Count);
        Assert.False(config.EnableMetrics);
        Assert.Equal("dotnet", config.MetricsProcessName);
        Assert.Equal(500, config.MetricsFrequencyInMs);
        Assert.Equal("dotnet", config.ProcessName);
        Assert.Equal("--version", config.ProcessArguments);
        Assert.Equal("/tmp", config.WorkingDirectory);
        Assert.Equal("1", config.EnvironmentVariables["ONE"]);
        Assert.Equal("2", config.EnvironmentVariables["TWO"]);
        Assert.Equal(new[] { "first.txt", "second.txt" }, config.PathValidations);
        Assert.Equal("value", config.Tags["stringTag"]);
        Assert.Equal(42, config.Tags["numericTag"]);
        Assert.Equal(15, config.Timeout.MaxDuration);
        Assert.Equal("dotnet-dump", config.Timeout.ProcessName);
        Assert.Equal("collect", config.Timeout.ProcessArguments);

        var scenario = Assert.Single(config.Scenarios);
        Assert.Equal("baseline", scenario.Name);
        Assert.True(scenario.IsBaseline);
        Assert.Equal("baseline", scenario.EnvironmentVariables["SCENARIO"]);
        Assert.Equal("scenarioValue", scenario.Tags["scenarioTag"]);
        Assert.Equal(typeof(DefaultAssertor).FullName, Assert.Single(config.Assertors).Type);
        Assert.Equal(typeof(JsonExporter).FullName, Assert.Single(config.Exporters).Type);
        Assert.Equal(typeof(NoopService).FullName, Assert.Single(config.Services).Type);
    }

    [Fact]
    public void Convenience_exporter_methods_are_idempotent()
    {
        var config = ConfigBuilder.Create()
            .WithJsonExporterPath("results.json")
            .WithJsonExporterPath("results.json")
            .WithExporter<JsonExporter>()
            .Build();

        Assert.Equal("results.json", config.JsonExporterFilePath);
        Assert.Single(config.Exporters);
        Assert.Equal(typeof(JsonExporter).FullName, config.Exporters[0].Type);
    }

    [Fact]
    public void Name_based_extensions_are_not_added_twice()
    {
        var config = ConfigBuilder.Create()
            .WithExporter("custom-exporter")
            .WithExporter("custom-exporter")
            .WithAssertor("custom-assertor")
            .WithAssertor("custom-assertor")
            .WithService("custom-service")
            .WithService("custom-service")
            .Build();

        Assert.Single(config.Exporters);
        Assert.Equal("custom-exporter", config.Exporters[0].Name);
        Assert.Single(config.Assertors);
        Assert.Equal("custom-assertor", config.Assertors[0].Name);
        Assert.Single(config.Services);
        Assert.Equal("custom-service", config.Services[0].Name);
    }

    [Fact]
    public void Built_in_exporter_aliases_are_not_duplicated_by_cli_overrides()
    {
        var config = new Config();
        config.Exporters.Add(new AssemblyLoadInfo { Name = "JsonExporter" });

        var builder = new ConfigBuilder(config).WithExporter<JsonExporter>();

        Assert.Single(builder.Build().Exporters);
    }

}

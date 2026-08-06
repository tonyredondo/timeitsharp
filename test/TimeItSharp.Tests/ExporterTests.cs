using System.Text.Json;
using TimeItSharp.Common.Configuration;
using TimeItSharp.Common.Exporters;
using TimeItSharp.Common.Results;

namespace TimeItSharp.Tests;

public sealed class ExporterTests
{
    [Fact]
    public void Json_exporter_redacts_sensitive_environment_values()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"timeitsharp-export-{Guid.NewGuid():N}.json");
        var config = new Config { JsonExporterFilePath = outputPath };
        var exporter = new JsonExporter();
        exporter.Initialize(new InitOptions(config, null, new TemplateVariables(), null));

        var scenario = new ScenarioResult { Name = "scenario" };
        scenario.EnvironmentVariables["API_TOKEN"] = "do-not-export";
        scenario.EnvironmentVariables["PATH"] = "/usr/bin";

        try
        {
            exporter.Export(new TimeitResult { Scenarios = [scenario] });

            using var document = JsonDocument.Parse(File.ReadAllText(outputPath));
            var environment = document.RootElement[0].GetProperty("environmentVariables");
            Assert.Equal("[REDACTED]", environment.GetProperty("API_TOKEN").GetString());
            Assert.Equal("/usr/bin", environment.GetProperty("PATH").GetString());
            Assert.Equal("do-not-export", scenario.EnvironmentVariables["API_TOKEN"]);
            Assert.Equal("/usr/bin", scenario.EnvironmentVariables["PATH"]);
        }
        finally
        {
            File.Delete(outputPath);
        }
    }

    [Fact]
    public void Json_exporter_omits_nonfinite_metric_values()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"timeitsharp-export-{Guid.NewGuid():N}.json");
        var config = new Config { JsonExporterFilePath = outputPath };
        var exporter = new JsonExporter();
        exporter.Initialize(new InitOptions(config, null, new TemplateVariables(), null));

        var dataPoint = new DataPoint();
        dataPoint.Metrics["bad"] = double.NaN;
        var scenario = new ScenarioResult { Name = "scenario", Data = [dataPoint] };
        scenario.Durations.Add(double.NaN);
        scenario.Durations.Add(1);
        scenario.Metrics["bad"] = double.PositiveInfinity;
        scenario.MetricsData["metric"] = [double.NaN, 2];
        scenario.AdditionalMetrics["bad"] = double.NegativeInfinity;
        scenario.Tags["bad"] = double.NaN;

        try
        {
            exporter.Export(new TimeitResult { Scenarios = [scenario] });

            using var document = JsonDocument.Parse(File.ReadAllText(outputPath));
            var exported = document.RootElement[0];
            Assert.Equal(1, exported.GetProperty("durations").GetArrayLength());
            Assert.False(exported.GetProperty("metrics").TryGetProperty("bad", out _));
            Assert.Equal(1, exported.GetProperty("metricsData").GetProperty("metric").GetArrayLength());
            Assert.False(exported.GetProperty("data")[0].GetProperty("metrics").TryGetProperty("bad", out _));
        }
        finally
        {
            File.Delete(outputPath);
        }
    }

    [Fact]
    public void Console_exporter_ignores_failed_scenario_distribution_series()
    {
        var config = new Config { Count = 10 };
        var exporter = new ConsoleExporter();
        exporter.Initialize(new InitOptions(config, null, new TemplateVariables(), null));
        var valid = new ScenarioResult { Name = "valid", Durations = [1] };
        var failed = new ScenarioResult { Name = "failed", Status = Status.Failed };

        var exception = Record.Exception(() => exporter.Export(new TimeitResult { Scenarios = [valid, failed] }));

        Assert.Null(exception);
    }

}

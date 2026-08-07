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
    public void Json_exporter_redacts_arguments_errors_nested_tags_and_output_without_mutating_input()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"timeitsharp-export-{Guid.NewGuid():N}.json");
        const string secret = "nested-secret-value";
        var config = new Config { JsonExporterFilePath = outputPath };
        var exporter = new JsonExporter();
        exporter.Initialize(new InitOptions(config, null, new TemplateVariables(), null));

        var nested = JsonSerializer.SerializeToElement(new
        {
            api_token = secret,
            ordinary = "safe",
            nested = new { password = secret }
        });
        var scenario = new ScenarioResult
        {
            Name = "scenario",
            ProcessArguments = $"--password {secret} --token=other-secret",
            Error = $"password={secret}; ordinary=safe",
            LastStandardOutput = string.Join("\n", Enumerable.Repeat(secret, 200)),
            Tags = new Dictionary<string, object>
            {
                ["nested"] = nested,
                ["nestedMap"] = new Dictionary<string, object>
                {
                    ["password"] = secret,
                    ["int"] = 42,
                    ["long"] = 42L,
                    ["date"] = DateTime.UtcNow
                },
                ["api_token"] = secret,
                ["ordinary"] = "safe"
            }
        };
        scenario.EnvironmentVariables["DATABASE_URL"] = $"postgres://user:{secret}@localhost/db";

        try
        {
            exporter.Export(new TimeitResult { Scenarios = [scenario] });
            var json = File.ReadAllText(outputPath);
            Assert.DoesNotContain(secret, json, StringComparison.Ordinal);
            Assert.DoesNotContain("other-secret", json, StringComparison.Ordinal);
            Assert.Contains("[REDACTED]", json, StringComparison.Ordinal);
            Assert.Equal($"--password {secret} --token=other-secret", scenario.ProcessArguments);
            Assert.Equal(secret, scenario.Tags["api_token"]);
            Assert.Equal(200, scenario.LastStandardOutput.Split('\n').Length);
        }
        finally
        {
            File.Delete(outputPath);
        }
    }

    [Fact]
    public void Json_exporter_redacts_template_values_after_expansion()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"timeitsharp-export-{Guid.NewGuid():N}.json");
        var variables = new TemplateVariables();
        variables.Add("SECRET_VALUE", "template-secret-value");
        var config = new Config { JsonExporterFilePath = outputPath };
        var exporter = new JsonExporter();
        exporter.Initialize(new InitOptions(config, null, variables, null));
        var scenario = new ScenarioResult
        {
            Name = "scenario",
            Tags = new Dictionary<string, object> { ["ordinary"] = "$(SECRET_VALUE)" }
        };

        try
        {
            exporter.Export(new TimeitResult { Scenarios = [scenario] });
            var json = File.ReadAllText(outputPath);
            Assert.DoesNotContain("template-secret-value", json, StringComparison.Ordinal);
            Assert.Contains(Utils.RedactedValue, json, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(outputPath);
        }
    }

    [Fact]
    public void Json_exporter_writes_bounded_output_and_handles_null_collections()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"timeitsharp-export-{Guid.NewGuid():N}.json");
        var config = new Config { JsonExporterFilePath = outputPath };
        var exporter = new JsonExporter();
        exporter.Initialize(new InitOptions(config, null, new TemplateVariables(), null));
        var malformed = new ScenarioResult { Name = "malformed" };
        malformed.Tags = null!;
        malformed.EnvironmentVariables = null!;
        malformed.PathValidations = null!;
        malformed.Data = null!;
        malformed.Durations = null!;
        malformed.Outliers = null!;
        malformed.Metrics = null!;
        malformed.MetricsData = null!;
        malformed.AdditionalMetrics = null!;
        malformed.LastStandardOutput = string.Join("\n", Enumerable.Repeat(new string('x', 10_000), 500));

        try
        {
            var exception = Record.Exception(() => exporter.Export(new TimeitResult { Scenarios = [malformed] }));
            Assert.Null(exception);
            Assert.True(File.Exists(outputPath));
            var document = JsonDocument.Parse(File.ReadAllText(outputPath));
            Assert.Equal(1, document.RootElement.GetArrayLength());
            Assert.DoesNotContain("NaN", File.ReadAllText(outputPath), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(outputPath);
        }
    }

    [Fact]
    public void Console_exporter_handles_extreme_finite_values_and_empty_metrics()
    {
        var exporter = new ConsoleExporter();
        exporter.Initialize(new InitOptions(new Config { Count = 10 }, null, new TemplateVariables(), null));
        var scenario = new ScenarioResult
        {
            Name = "[scenario]",
            Durations = [double.MinValue, double.MaxValue],
            MetricsData = new Dictionary<string, List<double>> { ["empty"] = [] }
        };

        var exception = Record.Exception(() => exporter.Export(new TimeitResult { Scenarios = [scenario] }));

        Assert.Null(exception);
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

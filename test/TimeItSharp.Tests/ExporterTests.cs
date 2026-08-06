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
}

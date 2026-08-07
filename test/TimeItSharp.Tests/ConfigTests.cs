using System.Text.Json;
using TimeItSharp.Common.Configuration;

namespace TimeItSharp.Tests;

public sealed class ConfigTests
{
    [Fact]
    public void NewConfiguration_has_safe_defaults()
    {
        var config = new Config();

        Assert.Equal(string.Empty, config.FilePath);
        Assert.Equal(string.Empty, config.Path);
        Assert.Equal(string.Empty, config.FileName);
        Assert.Equal(0, config.WarmUpCount);
        Assert.Equal(0, config.Count);
        Assert.Equal(200, config.MetricsFrequencyInMs);
        Assert.Equal(0.006, config.AcceptableRelativeWidth);
        Assert.Equal(0.95, config.ConfidenceLevel);
        Assert.Equal(45, config.MaximumDurationInMinutes);
        Assert.Equal(10, config.EvaluationInterval);
        Assert.Equal(0.001, config.MinimumErrorReduction);
        Assert.Empty(config.Scenarios);
        Assert.Empty(config.Exporters);
        Assert.Empty(config.Assertors);
        Assert.Empty(config.Services);
        Assert.NotNull(config.Timeout);
        Assert.NotNull(config.EnvironmentVariables);
        Assert.NotNull(config.PathValidations);
        Assert.NotNull(config.Tags);
    }

    [Fact]
    public void LoadConfiguration_reads_values_and_file_metadata()
    {
        var filePath = CreateTemporaryFile("{\n" +
            "  \"warmUpCount\": 2,\n" +
            "  \"count\": 4,\n" +
            "  \"processName\": \"dotnet\",\n" +
            "  \"processArguments\": \"--version\",\n" +
            "  \"scenarios\": [{ \"name\": \"baseline\", \"isBaseline\": true }],\n" +
            "  \"timeout\": { \"maxDuration\": 3, \"processName\": \"dotnet-dump\", \"processArguments\": \"collect\" }\n" +
            "}");

        try
        {
            var config = Config.LoadConfiguration(filePath);

            Assert.Equal(filePath, config.FilePath);
            Assert.Equal(Path.GetFileName(filePath), config.FileName);
            Assert.Equal(Path.GetDirectoryName(filePath), config.Path);
            Assert.Equal(2, config.WarmUpCount);
            Assert.Equal(4, config.Count);
            Assert.Equal("dotnet", config.ProcessName);
            Assert.Equal("--version", config.ProcessArguments);
            var scenario = Assert.Single(config.Scenarios);
            Assert.Equal("baseline", scenario.Name);
            Assert.True(scenario.IsBaseline);
            Assert.Equal(3, config.Timeout.MaxDuration);
            Assert.Equal("dotnet-dump", config.Timeout.ProcessName);
            Assert.Equal("collect", config.Timeout.ProcessArguments);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void LoadConfiguration_rejects_missing_file()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"timeitsharp-missing-{Guid.NewGuid():N}.json");

        Assert.Throws<FileNotFoundException>(() => Config.LoadConfiguration(missingPath));
    }

    [Fact]
    public void LoadConfiguration_rejects_invalid_json()
    {
        var filePath = CreateTemporaryFile("{ \"count\": ");

        try
        {
            Assert.Throws<JsonException>(() => Config.LoadConfiguration(filePath));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void Valid_configuration_passes_validation()
    {
        var config = new Config
        {
            Count = 1,
            ProcessName = "dotnet",
        };
        config.Scenarios.Add(new Scenario { Name = "default" });

        Assert.True(config.TryValidate(out var errors));
        Assert.Empty(errors);
        config.Validate();
    }

    [Fact]
    public void Validation_rejects_null_extension_collections()
    {
        var config = new Config
        {
            Count = 1,
            ProcessName = "echo",
            Exporters = null!,
            Assertors = null!,
            Services = null!,
        };
        config.Scenarios.Add(new Scenario { Name = "scenario" });

        Assert.False(config.TryValidate(out var errors));
        Assert.Contains("exporters cannot be null", errors);
        Assert.Contains("assertors cannot be null", errors);
        Assert.Contains("services cannot be null", errors);
    }

    [Fact]
    public void Invalid_configuration_reports_all_validation_errors()
    {
        var config = new Config
        {
            WarmUpCount = -1,
            Count = 0,
            ProcessName = " ",
            MetricsFrequencyInMs = -1,
            AcceptableRelativeWidth = double.NaN,
            ConfidenceLevel = 1,
            MaximumDurationInMinutes = -1,
            EvaluationInterval = 0,
            MinimumErrorReduction = -1,
            OverheadThreshold = -1,
        };
        var scenario = new Scenario { Name = " " };
        scenario.EnvironmentVariables.Add(string.Empty, "value");
        scenario.PathValidations.Add(" ");
        scenario.Timeout.MaxDuration = -1;
        scenario.Tags[string.Empty] = "value";
        config.Scenarios.Add(scenario);
        config.Exporters.Add(new AssemblyLoadInfo());

        Assert.False(config.TryValidate(out var errors));
        Assert.Contains("count must be greater than zero", errors);
        Assert.Contains("warmUpCount cannot be negative", errors);
        Assert.Contains("metricsFrequencyInMs must be greater than zero when metrics are enabled", errors);
        Assert.Contains("maximumDurationInMinutes must be greater than zero", errors);
        Assert.Contains("acceptableRelativeWidth must be a finite number greater than zero", errors);
        Assert.Contains("confidenceLevel must be a finite number between zero and one (exclusive)", errors);
        Assert.Contains("scenarios[0].name cannot be empty", errors);
        Assert.Contains("scenarios[0].processName is required when configuration.processName is not set", errors);
        Assert.Contains("exporters[0] must specify name or filePath", errors);
        Assert.Throws<ArgumentException>(() => config.Validate());
    }

    [Fact]
    public void LoadConfiguration_missing_parent_directory_throws_FileNotFoundException()
    {
        var filePath = Path.Combine(
            Path.GetTempPath(),
            $"timeitsharp-missing-directory-{Guid.NewGuid():N}",
            "config.json");

        var exception = Assert.Throws<FileNotFoundException>(() => Config.LoadConfiguration(filePath));

        Assert.Equal(filePath, exception.FileName);
    }

    [Fact]
    public void Validation_reports_root_string_length_diagnostics_once()
    {
        var overlong = new string('x', (64 * 1024) + 1);
        var config = new Config
        {
            Count = 1,
            EnableMetrics = false,
            ProcessName = overlong,
            ProcessArguments = overlong,
            WorkingDirectory = overlong,
        };
        config.Scenarios.Add(new Scenario { Name = "scenario" });

        var errors = config.GetValidationErrors();

        Assert.Equal(1, errors.Count(error => error == "configuration.processName cannot exceed 65536 characters"));
        Assert.Equal(1, errors.Count(error => error == "configuration.processArguments cannot exceed 65536 characters"));
        Assert.Equal(1, errors.Count(error => error == "configuration.workingDirectory cannot exceed 65536 characters"));
    }

    [Fact]
    public void Validation_accepts_maximum_runtime_supported_deadlines()
    {
        var config = new Config
        {
            Count = 1,
            EnableMetrics = false,
            ProcessName = "echo",
            MaximumDurationInMinutes = Config.MaxDurationMinutes,
        };
        config.Timeout.MaxDuration = Config.MaxTimeoutSeconds;
        config.Scenarios.Add(new Scenario
        {
            Name = "scenario",
            Timeout = new TimeItSharp.Common.Configuration.Timeout { MaxDuration = Config.MaxTimeoutSeconds },
        });

        Assert.True(config.TryValidate(out var errors));
        Assert.Empty(errors);
    }

    [Fact]
    public void Validation_rejects_deadlines_above_runtime_supported_CancelAfter_range()
    {
        var config = new Config
        {
            Count = 1,
            EnableMetrics = false,
            ProcessName = "echo",
            MaximumDurationInMinutes = Config.MaxDurationMinutes + 1,
        };
        config.Timeout.MaxDuration = Config.MaxTimeoutSeconds + 1;
        config.Scenarios.Add(new Scenario
        {
            Name = "scenario",
            Timeout = new TimeItSharp.Common.Configuration.Timeout { MaxDuration = Config.MaxTimeoutSeconds + 1 },
        });

        Assert.False(config.TryValidate(out var errors));
        Assert.Contains($"maximumDurationInMinutes cannot exceed {Config.MaxDurationMinutes}", errors);
        Assert.Contains($"configuration.timeout.maxDuration cannot exceed {Config.MaxTimeoutSeconds}", errors);
        Assert.Contains($"scenarios[0].timeout.maxDuration cannot exceed {Config.MaxTimeoutSeconds}", errors);
    }

    private static string CreateTemporaryFile(string contents)
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"timeitsharp-config-{Guid.NewGuid():N}.json");
        File.WriteAllText(filePath, contents);
        return filePath;
    }
}

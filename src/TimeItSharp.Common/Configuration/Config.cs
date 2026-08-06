using System.Text.Json;
using System.Text.Json.Serialization;

namespace TimeItSharp.Common.Configuration;

public class Config : ProcessData
{
    [JsonIgnore]
    public string FilePath { get; set; }

    [JsonIgnore]
    public string Path { get; set; }

    [JsonIgnore]
    public string FileName { get; set; }

    [JsonIgnore]
    public string Name { get; set; }

    [JsonPropertyName("warmUpCount")]
    public int WarmUpCount { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("enableDatadog")]
    public bool EnableDatadog { get; set; }

    [JsonPropertyName("enableMetrics")]
    public bool EnableMetrics { get; set; }

    [JsonPropertyName("metricsProcessName")]
    public string MetricsProcessName { get; set; }

    [JsonPropertyName("metricsFrequencyInMs")]
    public int MetricsFrequencyInMs { get; set; }

    [JsonPropertyName("scenarios")]
    public List<Scenario> Scenarios { get; set; }

    [JsonPropertyName("jsonExporterFilePath")]
    public string JsonExporterFilePath { get; set; }

    [JsonPropertyName("exporters")]
    public List<AssemblyLoadInfo> Exporters { get; set; }

    [JsonPropertyName("assertors")]
    public List<AssemblyLoadInfo> Assertors { get; set; }

    [JsonPropertyName("services")]
    public List<AssemblyLoadInfo> Services { get; set; }

    [JsonPropertyName("processFailedDataPoints")]
    public bool ProcessFailedDataPoints { get; set; }

    [JsonPropertyName("showStdOutForFirstRun")]
    public bool ShowStdOutForFirstRun { get; set; }

    [JsonPropertyName("debugMode")]
    public bool DebugMode { get; set; }

    [JsonPropertyName("acceptableRelativeWidth")]
    public double AcceptableRelativeWidth { get; set; }

    [JsonPropertyName("confidenceLevel")]
    public double ConfidenceLevel { get; set; }

    [JsonPropertyName("maximumDurationInMinutes")]
    public int MaximumDurationInMinutes { get; set; }

    [JsonPropertyName("evaluationInterval")]
    public int EvaluationInterval { get; set; }

    [JsonPropertyName("minimumErrorReduction")]
    public double MinimumErrorReduction { get; set; }

    [JsonPropertyName("overheadThreshold")]
    public double OverheadThreshold { get; set; }

    public Config()
    {
        FilePath = string.Empty;
        Path = string.Empty;
        FileName = string.Empty;
        Name = string.Empty;
        WarmUpCount = 0;
        Count = 0;
        EnableDatadog = false;
        EnableMetrics = true;
        MetricsProcessName = string.Empty;
        MetricsFrequencyInMs = 200;
        Scenarios = new();
        JsonExporterFilePath = string.Empty;
        Exporters = new();
        Assertors = new();
        Services = new();
        ProcessFailedDataPoints = false;
        ShowStdOutForFirstRun = false;
        DebugMode = false;
        AcceptableRelativeWidth = 0.006;
        ConfidenceLevel = 0.95;
        MaximumDurationInMinutes = 45;
        EvaluationInterval = 10;
        MinimumErrorReduction = 0.001;
        OverheadThreshold = 0;
    }

    /// <summary>
    /// Validates the configuration before it is cloned or executed.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when one or more configuration values are invalid.</exception>
    public void Validate()
    {
        var errors = GetValidationErrors();
        if (errors.Count == 0)
        {
            return;
        }

        throw new ArgumentException(
            $"The configuration is invalid:{Environment.NewLine} - {string.Join(Environment.NewLine + " - ", errors)}",
            nameof(Config));
    }

    /// <summary>
    /// Gets all validation errors without throwing. This is useful for CLI clients that want to
    /// display more than the first malformed setting.
    /// </summary>
    public IReadOnlyList<string> GetValidationErrors()
    {
        var errors = new List<string>();

        if (Count <= 0)
        {
            errors.Add("count must be greater than zero");
        }

        if (WarmUpCount < 0)
        {
            errors.Add("warmUpCount cannot be negative");
        }

        if (EnableMetrics && MetricsFrequencyInMs <= 0)
        {
            errors.Add("metricsFrequencyInMs must be greater than zero when metrics are enabled");
        }

        if (!double.IsFinite(AcceptableRelativeWidth) || AcceptableRelativeWidth <= 0)
        {
            errors.Add("acceptableRelativeWidth must be a finite number greater than zero");
        }

        if (!double.IsFinite(ConfidenceLevel) || ConfidenceLevel <= 0 || ConfidenceLevel >= 1)
        {
            errors.Add("confidenceLevel must be a finite number between zero and one (exclusive)");
        }

        if (MaximumDurationInMinutes <= 0)
        {
            errors.Add("maximumDurationInMinutes must be greater than zero");
        }
        else if (MaximumDurationInMinutes > TimeSpan.MaxValue.TotalMinutes)
        {
            errors.Add("maximumDurationInMinutes is too large");
        }

        if (EvaluationInterval <= 0)
        {
            errors.Add("evaluationInterval must be greater than zero");
        }

        if (!double.IsFinite(MinimumErrorReduction) || MinimumErrorReduction < 0)
        {
            errors.Add("minimumErrorReduction must be a finite number greater than or equal to zero");
        }

        if (!double.IsFinite(OverheadThreshold) || OverheadThreshold < 0)
        {
            errors.Add("overheadThreshold must be a finite number greater than or equal to zero");
        }

        ValidateProcessData(this, "configuration", errors);

        if (Scenarios is null)
        {
            errors.Add("scenarios cannot be null");
        }
        else if (Scenarios.Count == 0)
        {
            errors.Add("scenarios must contain at least one scenario");
        }
        else
        {
            for (var i = 0; i < Scenarios.Count; i++)
            {
                var scenario = Scenarios[i];
                if (scenario is null)
                {
                    errors.Add($"scenarios[{i}] cannot be null");
                    continue;
                }

                var prefix = $"scenarios[{i}]";
                if (string.IsNullOrWhiteSpace(scenario.Name))
                {
                    errors.Add($"{prefix}.name cannot be empty");
                }

                ValidateProcessData(scenario, prefix, errors);
                if (string.IsNullOrWhiteSpace(scenario.ProcessName) &&
                    string.IsNullOrWhiteSpace(ProcessName))
                {
                    errors.Add($"{prefix}.processName is required when configuration.processName is not set");
                }
            }
        }

        ValidateAssemblyLoadInfos(Exporters, "exporters", errors);
        ValidateAssemblyLoadInfos(Assertors, "assertors", errors);
        ValidateAssemblyLoadInfos(Services, "services", errors);

        return errors;
    }

    /// <summary>
    /// Attempts to validate the configuration without throwing.
    /// </summary>
    public bool TryValidate(out IReadOnlyList<string> errors)
    {
        errors = GetValidationErrors();
        return errors.Count == 0;
    }

    private static void ValidateProcessData(ProcessData processData, string prefix, ICollection<string> errors)
    {
        if (processData.EnvironmentVariables is null)
        {
            errors.Add($"{prefix}.environmentVariables cannot be null");
        }
        else
        {
            foreach (var item in processData.EnvironmentVariables)
            {
                if (string.IsNullOrWhiteSpace(item.Key))
                {
                    errors.Add($"{prefix}.environmentVariables cannot contain an empty name");
                }

                if (item.Value is null)
                {
                    errors.Add($"{prefix}.environmentVariables['{item.Key}'] cannot be null");
                }
            }
        }

        if (processData.PathValidations is null)
        {
            errors.Add($"{prefix}.pathValidations cannot be null");
        }
        else
        {
            for (var i = 0; i < processData.PathValidations.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(processData.PathValidations[i]))
                {
                    errors.Add($"{prefix}.pathValidations[{i}] cannot be empty");
                }
            }
        }

        if (processData.Timeout is null)
        {
            errors.Add($"{prefix}.timeout cannot be null");
        }
        else if (processData.Timeout.MaxDuration < 0)
        {
            errors.Add($"{prefix}.timeout.maxDuration cannot be negative");
        }

        if (processData.Tags is null)
        {
            errors.Add($"{prefix}.tags cannot be null");
        }
        else
        {
            foreach (var item in processData.Tags)
            {
                if (string.IsNullOrWhiteSpace(item.Key))
                {
                    errors.Add($"{prefix}.tags cannot contain an empty name");
                }
            }
        }
    }

    private static void ValidateAssemblyLoadInfos(
        IReadOnlyList<AssemblyLoadInfo>? extensionInfos,
        string propertyName,
        ICollection<string> errors)
    {
        // An empty collection means that the built-in extension is used. This is a supported
        // and important default, so only entries supplied by the caller are checked here.
        if (extensionInfos is null)
        {
            errors.Add($"{propertyName} cannot be null");
            return;
        }

        for (var i = 0; i < extensionInfos.Count; i++)
        {
            var info = extensionInfos[i];
            if (info is null)
            {
                errors.Add($"{propertyName}[{i}] cannot be null");
                continue;
            }

            if (info.InMemoryType is null &&
                string.IsNullOrWhiteSpace(info.Name) &&
                string.IsNullOrWhiteSpace(info.FilePath))
            {
                errors.Add($"{propertyName}[{i}] must specify name or filePath");
            }

            if (!string.IsNullOrWhiteSpace(info.FilePath) && string.IsNullOrWhiteSpace(info.Type))
            {
                errors.Add($"{propertyName}[{i}].type is required when filePath is specified");
            }
        }
    }

    public static Config LoadConfiguration(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("A configuration file path is required.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Configuration file not found.", filePath);
        }

#if NET5_0
        var jsonBytes = File.ReadAllBytes(filePath);
        if (JsonSerializer.Deserialize<Config>(jsonBytes) is { } config)
        {
            config.FilePath = filePath;
            config.FileName = System.IO.Path.GetFileName(filePath);
            config.Path = System.IO.Path.GetDirectoryName(filePath) ?? string.Empty;
            return config;
        }
#else
        using var fStream = File.OpenRead(filePath);
        if (JsonSerializer.Deserialize(fStream, ConfigContext.Default.Config) is { } config)
        {
            config.FilePath = filePath;
            config.FileName = System.IO.Path.GetFileName(filePath);
            config.Path = System.IO.Path.GetDirectoryName(filePath) ?? string.Empty;
            return config;
        }
#endif

        throw new JsonException($"Configuration file '{filePath}' is empty or contains a null value.");
    }

    internal override Config Clone() => new()
    {
        FilePath = FilePath,
        Path = Path,
        FileName = FileName,
        Name = Name,
        WarmUpCount = WarmUpCount,
        Count = Count,
        EnableDatadog = EnableDatadog,
        EnableMetrics = EnableMetrics,
        MetricsProcessName = MetricsProcessName,
        MetricsFrequencyInMs = MetricsFrequencyInMs,
        Scenarios = Scenarios.Any(s => s.IsBaseline) ? 
            Scenarios.Select(i => i.Clone()).OrderByDescending(s => s.IsBaseline).ToList() :
            Scenarios.Select(i => i.Clone()).ToList(),
        JsonExporterFilePath = JsonExporterFilePath,
        Exporters = Exporters.Select(i => i.Clone()).ToList(),
        Assertors = Assertors.Select(i => i.Clone()).ToList(),
        Services = Services.Select(i => i.Clone()).ToList(),
        ProcessName = ProcessName,
        ProcessArguments = ProcessArguments,
        WorkingDirectory = WorkingDirectory,
        EnvironmentVariables = new Dictionary<string, string>(EnvironmentVariables),
        PathValidations = new List<string>(PathValidations),
        Timeout = Timeout.Clone(),
        Tags = new Dictionary<string, object>(Tags),
        ProcessFailedDataPoints = ProcessFailedDataPoints,
        ShowStdOutForFirstRun = ShowStdOutForFirstRun,
        DebugMode = DebugMode,
        AcceptableRelativeWidth = AcceptableRelativeWidth,
        ConfidenceLevel = ConfidenceLevel,
        MaximumDurationInMinutes = MaximumDurationInMinutes,
        EvaluationInterval = EvaluationInterval,
        MinimumErrorReduction = MinimumErrorReduction,
        OverheadThreshold = OverheadThreshold,
    };
}

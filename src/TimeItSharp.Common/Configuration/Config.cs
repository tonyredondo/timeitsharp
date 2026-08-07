using System.Text.Json;
using System.Text.Json.Serialization;

namespace TimeItSharp.Common.Configuration;

public class Config : ProcessData
{
    internal const int MaxIterations = 100_000;
    internal const int MaxScenarioEntries = 1_024;
    private const int MaxConfigurationStringLength = 64 * 1024;
    private const int MaxConfigurationFileBytes = 16 * 1024 * 1024;
    private const int MaxOptionEntries = 1_024;
    private const int MaxEnvironmentEntries = 4_096;
    private const int MaxTagEntries = 1_024;
    private const int MaxExtensionEntries = 256;
    private const int MaxPathValidationEntries = 1_024;
    private const int MaxPathValidationLength = 64 * 1024;

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
        else if (Count > MaxIterations)
        {
            errors.Add($"count cannot exceed {MaxIterations}");
        }

        if (WarmUpCount < 0)
        {
            errors.Add("warmUpCount cannot be negative");
        }
        else if (WarmUpCount > MaxIterations)
        {
            errors.Add($"warmUpCount cannot exceed {MaxIterations}");
        }

        ValidateBoundedString(MetricsProcessName, "metricsProcessName", errors);
        ValidateBoundedString(JsonExporterFilePath, "jsonExporterFilePath", errors);
        ValidateBoundedString(FilePath, "filePath", errors);
        ValidateBoundedString(Path, "path", errors);
        ValidateBoundedString(FileName, "fileName", errors);
        ValidateBoundedString(Name, "name", errors);
        ValidateBoundedString(ProcessName, "configuration.processName", errors);
        ValidateBoundedString(ProcessArguments, "configuration.processArguments", errors);
        ValidateBoundedString(WorkingDirectory, "configuration.workingDirectory", errors);

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

        if (!double.IsFinite(MinimumErrorReduction) || MinimumErrorReduction < 0 || MinimumErrorReduction > 1)
        {
            errors.Add("minimumErrorReduction must be a finite number between zero and one (inclusive)");
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
        else if (Scenarios.Count > MaxScenarioEntries)
        {
            errors.Add($"scenarios cannot contain more than {MaxScenarioEntries} entries");
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
                ValidateBoundedString(scenario.Name, $"{prefix}.name", errors);

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

    /// <summary>
    /// Validates only the object graph invariants needed by a builder or clone operation.
    /// Semantic settings such as counts and scenario names are intentionally left to
    /// <see cref="Validate"/> so a fluent builder can be composed before it is complete.
    /// </summary>
    internal void ValidateStructure()
    {
        var errors = GetStructuralValidationErrors();
        if (errors.Count == 0)
        {
            return;
        }

        throw new ArgumentException(
            $"The configuration structure is invalid:{Environment.NewLine} - {string.Join(Environment.NewLine + " - ", errors)}",
            nameof(Config));
    }

    internal IReadOnlyList<string> GetStructuralValidationErrors()
    {
        var errors = new List<string>();
        ValidateProcessDataStructure(this, "configuration", errors);

        if (Scenarios is null)
        {
            errors.Add("scenarios cannot be null");
        }
        else
        {
            if (Scenarios.Count > MaxScenarioEntries)
            {
                errors.Add($"scenarios cannot contain more than {MaxScenarioEntries} entries");
            }

            for (var i = 0; i < Math.Min(Scenarios.Count, MaxScenarioEntries); i++)
            {
                if (Scenarios[i] is null)
                {
                    errors.Add($"scenarios[{i}] cannot be null");
                }
                else
                {
                    ValidateProcessDataStructure(Scenarios[i], $"scenarios[{i}]", errors);
                }
            }
        }

        ValidateAssemblyLoadInfos(Exporters, "exporters", errors);
        ValidateAssemblyLoadInfos(Assertors, "assertors", errors);
        ValidateAssemblyLoadInfos(Services, "services", errors);
        return errors;
    }

    internal static IReadOnlyList<string> GetAssemblyLoadInfoValidationErrors(
        AssemblyLoadInfo? info,
        string propertyName)
    {
        var errors = new List<string>();
        if (info is null)
        {
            errors.Add($"{propertyName} cannot be null");
            return errors;
        }

        ValidateBoundedString(info.FilePath, $"{propertyName}.filePath", errors);
        ValidateBoundedString(info.Type, $"{propertyName}.type", errors);
        ValidateBoundedString(info.Name, $"{propertyName}.name", errors);
        if (info.Options is not null)
        {
            if (info.Options.Count > MaxOptionEntries)
            {
                errors.Add($"{propertyName}.options cannot contain more than {MaxOptionEntries} entries");
            }

            foreach (var option in info.Options.Take(MaxOptionEntries))
            {
                ValidateBoundedString(option.Key, $"{propertyName}.options key", errors);
                try
                {
                    if (option.Value is JsonElement element && element.GetRawText().Length > MaxConfigurationStringLength)
                    {
                        errors.Add($"{propertyName}.options value cannot exceed {MaxConfigurationStringLength} characters");
                    }
                }
                catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or ObjectDisposedException)
                {
                    errors.Add($"{propertyName}.options contains an invalid JSON value");
                }
            }
        }

        var hasName = !string.IsNullOrWhiteSpace(info.Name);
        var hasFilePath = !string.IsNullOrWhiteSpace(info.FilePath);
        var hasType = !string.IsNullOrWhiteSpace(info.Type);
        var hasInMemoryType = info.InMemoryType is not null;

        if (info.Name is not null && !hasName)
        {
            errors.Add($"{propertyName}.name cannot be empty");
        }

        if (hasInMemoryType)
        {
            // Fluent type registrations carry both the in-memory type and its assembly metadata
            // for single-file/diagnostic scenarios. Name is still ambiguous and is rejected.
            if (hasName)
            {
                errors.Add($"{propertyName} cannot combine name with inMemoryType");
            }

            if (hasFilePath != hasType)
            {
                errors.Add($"{propertyName}.type and filePath must be specified together when inMemoryType is set");
            }

            return errors;
        }

        if (!hasName && !hasFilePath && !hasType)
        {
            // Preserve the established diagnostic for an entirely empty entry.
            errors.Add($"{propertyName} must specify name or filePath");
        }
        else if (hasName)
        {
            if (hasFilePath || hasType)
            {
                errors.Add($"{propertyName} name cannot be combined with filePath or type");
            }
        }
        else if (hasFilePath && !hasType)
        {
            errors.Add($"{propertyName}.type is required when filePath is specified");
        }
        else if (hasType && !hasFilePath)
        {
            errors.Add($"{propertyName}.filePath is required when type is specified");
        }

        return errors;
    }

    private static void ValidateBoundedString(string? value, string propertyName, ICollection<string> errors)
    {
        if (value is not null && value.Length > MaxConfigurationStringLength)
        {
            errors.Add($"{propertyName} cannot exceed {MaxConfigurationStringLength} characters");
        }
    }

    private static void ValidateProcessDataStructure(ProcessData processData, string prefix, ICollection<string> errors)
    {
        if (processData.EnvironmentVariables is null)
        {
            errors.Add($"{prefix}.environmentVariables cannot be null");
        }

        if (processData.PathValidations is null)
        {
            errors.Add($"{prefix}.pathValidations cannot be null");
        }

        if (processData.Timeout is null)
        {
            errors.Add($"{prefix}.timeout cannot be null");
        }

        if (processData.Tags is null)
        {
            errors.Add($"{prefix}.tags cannot be null");
        }
    }

    private static void ValidateProcessData(ProcessData processData, string prefix, ICollection<string> errors)
    {
        ValidateBoundedString(processData.ProcessName, $"{prefix}.processName", errors);
        ValidateBoundedString(processData.ProcessArguments, $"{prefix}.processArguments", errors);
        ValidateBoundedString(processData.WorkingDirectory, $"{prefix}.workingDirectory", errors);

        if (processData.EnvironmentVariables is null)
        {
            errors.Add($"{prefix}.environmentVariables cannot be null");
        }
        else
        {
            if (processData.EnvironmentVariables.Count > MaxEnvironmentEntries)
            {
                errors.Add($"{prefix}.environmentVariables cannot contain more than {MaxEnvironmentEntries} entries");
            }

            var environmentNames = new HashSet<string>(
                OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
            foreach (var item in processData.EnvironmentVariables.Take(MaxEnvironmentEntries))
            {
                if (string.IsNullOrWhiteSpace(item.Key))
                {
                    errors.Add($"{prefix}.environmentVariables cannot contain an empty name");
                }
                else if (item.Key.Length > MaxConfigurationStringLength)
                {
                    errors.Add($"{prefix}.environmentVariables name cannot exceed {MaxConfigurationStringLength} characters");
                }
                else if (item.Value is not null && item.Value.Length > MaxConfigurationStringLength)
                {
                    errors.Add($"{prefix}.environmentVariables value cannot exceed {MaxConfigurationStringLength} characters");
                }
                else if (!environmentNames.Add(item.Key))
                {
                    errors.Add($"{prefix}.environmentVariables contains duplicate names differing only by case: '{item.Key}'");
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
            if (processData.PathValidations.Count > MaxPathValidationEntries)
            {
                errors.Add($"{prefix}.pathValidations cannot contain more than {MaxPathValidationEntries} entries");
            }

            for (var i = 0; i < Math.Min(processData.PathValidations.Count, MaxPathValidationEntries); i++)
            {
                var path = processData.PathValidations[i];
                if (string.IsNullOrWhiteSpace(path))
                {
                    errors.Add($"{prefix}.pathValidations[{i}] cannot be empty");
                }
                else if (path.Length > MaxPathValidationLength)
                {
                    errors.Add($"{prefix}.pathValidations[{i}] cannot exceed {MaxPathValidationLength} characters");
                }
            }
        }

        if (processData.Timeout is null)
        {
            errors.Add($"{prefix}.timeout cannot be null");
        }
        else
        {
            ValidateBoundedString(processData.Timeout.ProcessName, $"{prefix}.timeout.processName", errors);
            ValidateBoundedString(processData.Timeout.ProcessArguments, $"{prefix}.timeout.processArguments", errors);
            if (processData.Timeout.MaxDuration < 0)
            {
                errors.Add($"{prefix}.timeout.maxDuration cannot be negative");
            }
        }

        if (processData.Tags is null)
        {
            errors.Add($"{prefix}.tags cannot be null");
        }
        else
        {
            if (processData.Tags.Count > MaxTagEntries)
            {
                errors.Add($"{prefix}.tags cannot contain more than {MaxTagEntries} entries");
            }

            foreach (var item in processData.Tags.Take(MaxTagEntries))
            {
                if (string.IsNullOrWhiteSpace(item.Key))
                {
                    errors.Add($"{prefix}.tags cannot contain an empty name");
                }
                ValidateBoundedString(item.Key, $"{prefix}.tags key", errors);
                try
                {
                    var valueLength = item.Value switch
                    {
                        string text => text.Length,
                        JsonElement element => element.GetRawText().Length,
                        _ => 0,
                    };
                    if (valueLength > MaxConfigurationStringLength)
                    {
                        errors.Add($"{prefix}.tags value cannot exceed {MaxConfigurationStringLength} characters");
                    }
                }
                catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or ObjectDisposedException)
                {
                    errors.Add($"{prefix}.tags contains an invalid JSON value");
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

        if (extensionInfos.Count > MaxExtensionEntries)
        {
            errors.Add($"{propertyName} cannot contain more than {MaxExtensionEntries} entries");
        }

        for (var i = 0; i < Math.Min(extensionInfos.Count, MaxExtensionEntries); i++)
        {
            var info = extensionInfos[i];
            if (info is null)
            {
                errors.Add($"{propertyName}[{i}] cannot be null");
                continue;
            }

            foreach (var error in GetAssemblyLoadInfoValidationErrors(info, $"{propertyName}[{i}]"))
            {
                errors.Add(error);
            }
        }
    }

    public static Config LoadConfiguration(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("A configuration file path is required.", nameof(filePath));
        }
        if (filePath.Length > MaxConfigurationStringLength)
        {
            throw new ArgumentException($"A configuration file path cannot exceed {MaxConfigurationStringLength} characters.", nameof(filePath));
        }

        FileStream fStream;
        try
        {
            // Open once and inspect this same handle so a replacement after File.Exists cannot
            // bypass the configuration size limit.
            fStream = File.OpenRead(filePath);
        }
        catch (FileNotFoundException)
        {
            throw new FileNotFoundException("Configuration file not found.", filePath);
        }

        using (fStream)
        {
            if (fStream.Length > MaxConfigurationFileBytes)
            {
                throw new InvalidDataException($"Configuration file exceeds the {MaxConfigurationFileBytes} byte limit.");
            }

#if NET5_0
            var jsonBytes = new byte[(int)fStream.Length];
            var offset = 0;
            while (offset < jsonBytes.Length)
            {
                var read = fStream.Read(jsonBytes, offset, jsonBytes.Length - offset);
                if (read == 0)
                {
                    break;
                }

                offset += read;
            }

            if (JsonSerializer.Deserialize<Config>(jsonBytes) is { } config)
            {
                config.FilePath = filePath;
                config.FileName = System.IO.Path.GetFileName(filePath);
                config.Path = System.IO.Path.GetDirectoryName(filePath) ?? string.Empty;
                return config;
            }
#else
            if (JsonSerializer.Deserialize(fStream, ConfigContext.Default.Config) is { } config)
            {
                config.FilePath = filePath;
                config.FileName = System.IO.Path.GetFileName(filePath);
                config.Path = System.IO.Path.GetDirectoryName(filePath) ?? string.Empty;
                return config;
            }
#endif
        }

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

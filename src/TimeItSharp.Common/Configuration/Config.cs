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

    public static Config LoadConfiguration(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Configuration file not found.");
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
        if (JsonSerializer.Deserialize<Config>(fStream, ConfigContext.Default.Config) is { } config)
        {
            config.FilePath = filePath;
            config.FileName = System.IO.Path.GetFileName(filePath);
            config.Path = System.IO.Path.GetDirectoryName(filePath) ?? string.Empty;
            return config;
        }
#endif

        return new Config();
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
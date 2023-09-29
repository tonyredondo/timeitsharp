// using Newtonsoft.Json;

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

    [JsonPropertyName("warmUpCount")]
    public int WarmUpCount { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("enableDatadog")]
    public bool EnableDatadog { get; set; }
    
    [JsonPropertyName("enableMetrics")]
    public bool EnableMetrics { get; set; }

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
    
    public Config()
    {
        FilePath = string.Empty;
        Path = string.Empty;
        FileName = string.Empty;
        WarmUpCount = 0;
        Count = 0;
        EnableDatadog = false;
        EnableMetrics = true;
        Scenarios = new();
        JsonExporterFilePath = string.Empty;
        Exporters = new();
        Assertors = new();
        Services = new();
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
        if (JsonSerializer.Deserialize<Config>(fStream) is { } config)
        {
            config.FilePath = filePath;
            config.FileName = System.IO.Path.GetFileName(filePath);
            config.Path = System.IO.Path.GetDirectoryName(filePath) ?? string.Empty;
            return config;
        }
#endif

        return new Config();
    }
}
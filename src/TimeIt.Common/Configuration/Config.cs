using Newtonsoft.Json;

namespace TimeIt.Common.Configuration;

public class Config : ProcessData
{
    [JsonIgnore]
    public string FilePath { get; set; }
    [JsonIgnore]
    public string Path { get; set; }
    [JsonIgnore]
    public string FileName { get; set; }

    [JsonProperty("warmUpCount")]
    public int WarmUpCount { get; set; }
    [JsonProperty("count")]
    public int Count { get; set; }
    [JsonProperty("enableDatadog")]
    public bool EnableDatadog { get; set; }
    [JsonProperty("scenarios")]
    public List<Scenario> Scenarios { get; set; }
    [JsonProperty("jsonExporterFilePath")]
    public string JsonExporterFilePath { get; set; }

    public Config()
    {
        FilePath = string.Empty;
        Path = string.Empty;
        FileName = string.Empty;
        WarmUpCount = 0;
        Count = 0;
        EnableDatadog = false;
        Scenarios = new List<Scenario>();
        JsonExporterFilePath = string.Empty;
    }

    public static Config LoadConfiguration(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Configuration file not found.");
        }

        var serializer = new JsonSerializer();
        using var fStream = File.OpenRead(filePath);
        using var sReader = new StreamReader(fStream);
        using var jsonReader = new JsonTextReader(sReader);

        if (serializer.Deserialize<Config>(jsonReader) is { } config)
        {
            config.FilePath = filePath;
            config.FileName = System.IO.Path.GetFileName(filePath);
            config.Path = System.IO.Path.GetDirectoryName(filePath) ?? string.Empty;
            return config;
        }

        return new Config();
    }
}
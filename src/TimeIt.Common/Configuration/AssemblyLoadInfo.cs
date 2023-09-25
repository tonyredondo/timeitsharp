using System.Text.Json.Serialization;

namespace TimeIt.Common.Configuration;

public class AssemblyLoadInfo
{
    [JsonPropertyName("filePath")]
    public string? FilePath { get; set; }
    
    [JsonPropertyName("type")]
    public string? Type { get; set; }
    
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}
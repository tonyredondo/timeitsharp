using System.Text.Json.Serialization;

namespace TimeItSharp.Common.Configuration;

public class AssemblyLoadInfo
{
    [JsonPropertyName("filePath")]
    public string? FilePath { get; set; }
    
    [JsonPropertyName("type")]
    public string? Type { get; set; }
    
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    internal virtual AssemblyLoadInfo Clone() => new AssemblyLoadInfo
    {
        FilePath = FilePath,
        Type = Type,
        Name = Name,
    };
}
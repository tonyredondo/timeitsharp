using System.Text.Json;
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
    
    [JsonPropertyName("options")]
    public Dictionary<string, JsonElement?>? Options { get; set; }
    
    [JsonIgnore]
    public Type? InMemoryType { get; set; }
    
    internal AssemblyLoadInfo Clone() => new()
    {
        FilePath = FilePath,
        Type = Type,
        Name = Name,
        Options = Options,
        InMemoryType = InMemoryType,
    };
}

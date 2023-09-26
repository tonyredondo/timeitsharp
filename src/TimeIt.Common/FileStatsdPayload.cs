using System.Text.Json.Serialization;

namespace TimeIt.Common;

public sealed class FileStatsdPayload
{
    public FileStatsdPayload(string type, string name, double value)
    {
        Type = type;
        Name = name;
        Value = value;
    }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("value")]
    public double Value { get; set; }
}
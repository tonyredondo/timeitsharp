using System.Text.Json.Serialization;

namespace TimeItSharp.Common;

internal sealed class FileStatsdPayload(string type, string name, double value)
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = type;

    [JsonPropertyName("name")]
    public string Name { get; set; } = name;

    [JsonPropertyName("value")]
    public double Value { get; set; } = value;
}
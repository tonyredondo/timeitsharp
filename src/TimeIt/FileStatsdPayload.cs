using Newtonsoft.Json;

namespace TimeIt;

public sealed class FileStatsdPayload
{
    public FileStatsdPayload(string type, string name, double value)
    {
        Type = type;
        Name = name;
        Value = value;
    }

    [JsonProperty("type")]
    public string? Type { get; set; }
    [JsonProperty("name")]
    public string? Name { get; set; }
    [JsonProperty("value")]
    public double Value { get; set; }
}

using System.Text.Json.Serialization;

namespace TimeItSharp.Common.Configuration;

public class Timeout
{
    [JsonPropertyName("maxDuration")]
    public int MaxDuration { get; set; }

    [JsonPropertyName("processName")]
    public string? ProcessName { get; set; }

    [JsonPropertyName("processArguments")]
    public string? ProcessArguments { get; set; }

    public Timeout()
    {
        MaxDuration = 0;
    }

    public Timeout(int maxDuration, string? processName, string? processArguments)
    {
        MaxDuration = maxDuration;
        ProcessName = processName;
        ProcessArguments = processArguments;
    }
}
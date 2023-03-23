using System.Text.Json.Serialization;

namespace TimeIt.Common.Configuration;

public class ProcessData
{
    [JsonPropertyName("processName")]
    public string? ProcessName { get; set; }

    [JsonPropertyName("processArguments")]
    public string? ProcessArguments { get; set; }

    [JsonPropertyName("workingDirectory")]
    public string? WorkingDirectory { get; set; }

    [JsonPropertyName("environmentVariables")]
    public Dictionary<string, string> EnvironmentVariables { get; set; }

    [JsonPropertyName("pathValidations")]
    public List<string> PathValidations { get; set; }
    
    [JsonPropertyName("timeout")]
    public Timeout Timeout { get; set; }

    [JsonPropertyName("tags")]
    public Dictionary<string, string> Tags { get; set; }

    public ProcessData()
    {
        EnvironmentVariables = new Dictionary<string, string>();
        PathValidations = new List<string>();
        Timeout = new Timeout();
        Tags = new Dictionary<string, string>();
    }

    public ProcessData(string? processName, string? processArguments, string? workingDirectory,
        Dictionary<string, string> environmentVariables, List<string> pathValidations, Timeout timeout, Dictionary<string, string> tags)
    {
        ProcessName = processName;
        ProcessArguments = processArguments;
        WorkingDirectory = workingDirectory;
        EnvironmentVariables = environmentVariables;
        PathValidations = pathValidations;
        Timeout = timeout;
        Tags = tags;
    }
}
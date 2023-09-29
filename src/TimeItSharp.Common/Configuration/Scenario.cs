using System.Text.Json.Serialization;

namespace TimeItSharp.Common.Configuration;

public class Scenario : ProcessData
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    public Scenario()
    {
        Name = string.Empty;
    }

    public Scenario(string name, ProcessData? processData = null)
    {
        Name = name;
        if (processData is not null)
        {
            ProcessName = processData.ProcessName;
            ProcessArguments = processData.ProcessArguments;
            WorkingDirectory = processData.WorkingDirectory;
            EnvironmentVariables = processData.EnvironmentVariables;
            PathValidations = processData.PathValidations;
            Timeout = processData.Timeout;
            Tags = processData.Tags;
        }
    }
}
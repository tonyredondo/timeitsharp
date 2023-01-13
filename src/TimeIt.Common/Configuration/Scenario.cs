using Newtonsoft.Json;

namespace TimeIt.Common.Configuration;

public class Scenario : ProcessData
{
    [JsonProperty("name")]
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
            Timeout = processData.Timeout;
            Tags = processData.Tags;
        }
    }
}
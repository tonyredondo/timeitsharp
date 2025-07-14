using System.Text.Json.Serialization;
using TimeItSharp.Common.Services;

namespace TimeItSharp.Common.Configuration;

public class Scenario : ProcessData
{
    [JsonPropertyName("name")]
    public string Name { get; set; }
    
    [JsonPropertyName("isBaseline")]
    public bool IsBaseline { get; set; }
    
    public IService? ParentService { get; set; }

    public Scenario()
    {
        Name = string.Empty;
    }

    public Scenario(string name, bool isBaseline, ProcessData? processData = null)
    {
        Name = name;
        IsBaseline = isBaseline;
        ParentService = null;
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

    internal override Scenario Clone() => new()
    {
        Name = Name,
        IsBaseline = IsBaseline,
        ParentService = ParentService,
        ProcessName = ProcessName,
        ProcessArguments = ProcessArguments,
        WorkingDirectory = WorkingDirectory,
        EnvironmentVariables = new Dictionary<string, string>(EnvironmentVariables),
        PathValidations = new List<string>(PathValidations),
        Timeout = Timeout.Clone(),
        Tags = new Dictionary<string, object>(Tags),
    };
}
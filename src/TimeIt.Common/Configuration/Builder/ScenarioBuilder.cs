namespace TimeIt.Common.Configuration.Builder;

public sealed class ScenarioBuilder
{
    private readonly Scenario _scenario;

    public ScenarioBuilder()
    {
        _scenario = new();
    }

    public ScenarioBuilder(Scenario scenario)
    {
        _scenario = scenario;
    }

    public static ScenarioBuilder Create() => new();

    public Scenario Build() => _scenario;

    public ScenarioBuilder WithName(string name)
    {
        _scenario.Name = name;
        return this;
    }
    
    #region ProcessData

    public ScenarioBuilder WithProcessName(string? processName)
    {
        _scenario.ProcessName = processName;
        return this;
    }

    public ScenarioBuilder WithProcessArguments(string? processArguments)
    {
        _scenario.ProcessArguments = processArguments;
        return this;
    }
    
    public ScenarioBuilder WithWorkingDirectory(string? workingDirectory)
    {
        _scenario.WorkingDirectory = workingDirectory;
        return this;
    }

    public ScenarioBuilder WithEnvironmentVariables(Dictionary<string, string> environmentVariables)
    {
        foreach (var kv in environmentVariables)
        {
            _scenario.EnvironmentVariables[kv.Key] = kv.Value;
        }

        return this;
    }
    
    public ScenarioBuilder WithEnvironmentVariable(string key, string value)
    {
        _scenario.EnvironmentVariables[key] = value;
        return this;
    }

    public ScenarioBuilder WithPathValidations(params string[] pathValidations)
    {
        _scenario.PathValidations.AddRange(pathValidations);
        return this;
    }

    public ScenarioBuilder WithPathValidations(string pathValidation)
    {
        _scenario.PathValidations.Add(pathValidation);
        return this;
    }

    public ScenarioBuilder WithTimeout(TimeoutBuilder timeoutBuilder)
    {
        _scenario.Timeout = timeoutBuilder.Build();
        return this;
    }

    public ScenarioBuilder WithTimeout(Func<TimeoutBuilder, TimeoutBuilder> timeoutBuilderFunc)
    {
        return WithTimeout(timeoutBuilderFunc(new TimeoutBuilder(_scenario.Timeout)));
    }
    
    public ScenarioBuilder WithTags(Dictionary<string, string> tags)
    {
        foreach (var kv in tags)
        {
            _scenario.Tags[kv.Key] = kv.Value;
        }

        return this;
    }
    
    public ScenarioBuilder WithTags(string key, string value)
    {
        _scenario.Tags[key] = value;
        return this;
    }

    #endregion
}
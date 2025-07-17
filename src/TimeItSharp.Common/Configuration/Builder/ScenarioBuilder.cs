namespace TimeItSharp.Common.Configuration.Builder;

/// <summary>
/// Scenario configuration builder
/// </summary>
public sealed class ScenarioBuilder
{
    private readonly Scenario _scenario;

    /// <summary>
    /// Creates a new instance of the Scenario configuration builder
    /// </summary>
    public ScenarioBuilder()
    {
        _scenario = new();
    }

    /// <summary>
    /// Creates a new instance of the Scenario configuration builder
    /// </summary>
    /// <param name="scenario">Existing scenario instance</param>
    public ScenarioBuilder(Scenario scenario)
    {
        _scenario = scenario;
    }

    /// <summary>
    /// Creates a new instance of the Scenario configuration builder
    /// </summary>
    /// <returns>ScenarioBuilder instance</returns>
    public static ScenarioBuilder Create() => new();

    /// <summary>
    /// Build the configuration from the builder
    /// </summary>
    /// <returns>Scenario instance</returns>
    public Scenario Build() => _scenario;

    /// <summary>
    /// Sets the name of the scenario
    /// </summary>
    /// <param name="name">Scenario name</param>
    /// <returns>Scenario builder instance</returns>
    public ScenarioBuilder WithName(string name)
    {
        _scenario.Name = name;
        return this;
    }

    /// <summary>
    /// Sets the scenario as a baseline scenario.
    /// </summary>
    /// <returns>Scenario builder instance</returns>
    public ScenarioBuilder AsBaseline()
    {
        _scenario.IsBaseline = true;
        return this;
    }
    
    #region ProcessData

    /// <summary>
    /// Sets the process name
    /// </summary>
    /// <param name="processName">Process name to be executed</param>
    /// <returns>Scenario builder instance</returns>
    public ScenarioBuilder WithProcessName(string? processName)
    {
        _scenario.ProcessName = processName;
        return this;
    }

    /// <summary>
    /// Sets the process arguments
    /// </summary>
    /// <param name="processArguments">Process arguments</param>
    /// <returns>Scenario builder instance</returns>
    public ScenarioBuilder WithProcessArguments(string? processArguments)
    {
        _scenario.ProcessArguments = processArguments;
        return this;
    }
    
    /// <summary>
    /// Sets the working directory
    /// </summary>
    /// <param name="workingDirectory">Working directory</param>
    /// <returns>Scenario builder instance</returns>
    public ScenarioBuilder WithWorkingDirectory(string? workingDirectory)
    {
        _scenario.WorkingDirectory = workingDirectory;
        return this;
    }

    /// <summary>
    /// Adds process environment variables
    /// </summary>
    /// <param name="environmentVariables">Environment variables dictionary</param>
    /// <returns>Scenario builder instance</returns>
    public ScenarioBuilder WithEnvironmentVariables(Dictionary<string, string> environmentVariables)
    {
        foreach (var kv in environmentVariables)
        {
            _scenario.EnvironmentVariables[kv.Key] = kv.Value;
        }

        return this;
    }
    
    /// <summary>
    /// Adds a process environment variable
    /// </summary>
    /// <param name="name">Environment variable name</param>
    /// <param name="value">Environment variable value</param>
    /// <returns>Scenario builder instance</returns>
    public ScenarioBuilder WithEnvironmentVariable(string name, string value)
    {
        _scenario.EnvironmentVariables[name] = value;
        return this;
    }

    /// <summary>
    /// Adds paths to be validated before running the scenario
    /// </summary>
    /// <param name="pathValidations">Paths validations array</param>
    /// <returns>Scenario builder instance</returns>
    public ScenarioBuilder WithPathValidations(params string[] pathValidations)
    {
        _scenario.PathValidations.AddRange(pathValidations);
        return this;
    }

    /// <summary>
    /// Adds a path to be validated before running the scenario
    /// </summary>
    /// <param name="pathValidation">Path to be validated</param>
    /// <returns>Scenario builder instance</returns>
    public ScenarioBuilder WithPathValidations(string pathValidation)
    {
        _scenario.PathValidations.Add(pathValidation);
        return this;
    }

    /// <summary>
    /// Sets the timeout configuration
    /// </summary>
    /// <param name="timeoutBuilder">Timeout builder instance</param>
    /// <returns>Scenario builder instance</returns>
    public ScenarioBuilder WithTimeout(TimeoutBuilder timeoutBuilder)
    {
        _scenario.Timeout = timeoutBuilder.Build();
        return this;
    }

    /// <summary>
    /// Sets the timeout configuration
    /// </summary>
    /// <param name="timeoutBuilderFunc">Timeout builder delegate</param>
    /// <returns>Scenario builder instance</returns>
    public ScenarioBuilder WithTimeout(Func<TimeoutBuilder, TimeoutBuilder> timeoutBuilderFunc)
    {
        return WithTimeout(timeoutBuilderFunc(new TimeoutBuilder(_scenario.Timeout)));
    }
    
    /// <summary>
    /// Adds tags to each scenario
    /// </summary>
    /// <param name="tags">Tags dictionary</param>
    /// <returns>Scenario builder instance</returns>
    public ScenarioBuilder WithTags(Dictionary<string, string> tags)
    {
        foreach (var kv in tags)
        {
            _scenario.Tags[kv.Key] = kv.Value;
        }

        return this;
    }
    
    /// <summary>
    /// Adds a tag to each scenario
    /// </summary>
    /// <param name="key">Key of the tag</param>
    /// <param name="value">Value of the tag</param>
    /// <returns>Scenario builder instance</returns>
    public ScenarioBuilder WithTags(string key, string value)
    {
        _scenario.Tags[key] = value;
        return this;
    }

    #endregion
}
namespace TimeItSharp.Common.Configuration.Builder;

/// <summary>
/// Timeout configuration builder
/// </summary>
public sealed class TimeoutBuilder
{
    private readonly Timeout _timeout;

    /// <summary>
    /// Creates a new instance of the Timeout configuration builder
    /// </summary>
    public TimeoutBuilder()
    {
        _timeout = new();
    }

    /// <summary>
    /// Creates a new instance of the Timeout configuration builder
    /// </summary>
    /// <param name="timeout">Existing timeout instance</param>
    public TimeoutBuilder(Timeout timeout)
    {
        _timeout = timeout;
    }
    
    /// <summary>
    /// Creates a new instance of the Timeout configuration builder
    /// </summary>
    /// <returns>TimeoutBuilder instance</returns>
    public static TimeoutBuilder Create() => new();

    /// <summary>
    /// Build the configuration from the builder
    /// </summary>
    /// <returns>Timeout instance</returns>
    public Timeout Build() => _timeout;

    /// <summary>
    /// Sets the max duration / timeout of the program running
    /// </summary>
    /// <param name="maxDuration">Timeout value in seconds</param>
    /// <returns>Timeout builder instance</returns>
    public TimeoutBuilder WithMaxDuration(int maxDuration)
    {
        _timeout.MaxDuration = maxDuration;
        return this;
    }
    
    /// <summary>
    /// Sets the process name to run when a timeout occurs
    /// </summary>
    /// <param name="processName">Process name</param>
    /// <returns>Timeout builder instance</returns>
    public TimeoutBuilder WithProcessName(string? processName)
    {
        _timeout.ProcessName = processName;
        return this;
    }

    /// <summary>
    /// Sets the process arguments
    /// </summary>
    /// <param name="processArguments">Process arguments</param>
    /// <returns>Timeout builder instance</returns>
    public TimeoutBuilder WithProcessArguments(string? processArguments)
    {
        _timeout.ProcessArguments = processArguments;
        return this;
    }
}
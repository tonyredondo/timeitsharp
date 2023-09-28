namespace TimeIt.Common.Configuration.Builder;

public sealed class TimeoutBuilder
{
    private readonly Timeout _timeout;

    public TimeoutBuilder()
    {
        _timeout = new();
    }

    public TimeoutBuilder(Timeout timeout)
    {
        _timeout = timeout;
    }
    
    public Timeout Build() => _timeout;

    public TimeoutBuilder WithMaxDuration(int maxDuration)
    {
        _timeout.MaxDuration = maxDuration;
        return this;
    }
    
    public TimeoutBuilder WithProcessName(string? processName)
    {
        _timeout.ProcessName = processName;
        return this;
    }

    public TimeoutBuilder WithProcessArguments(string? processArguments)
    {
        _timeout.ProcessArguments = processArguments;
        return this;
    }
}
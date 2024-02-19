namespace TimeItSharp.Common.Services;

public sealed class DatadogProfilerConfiguration
{
    private Dictionary<string, bool>? _enabledScenarios = null;
    private bool _runAtCoolDown = false;
    private int _coolDownCount = 0;

    internal bool HasToRunAtCoolDown => _runAtCoolDown;

    internal int CoolDownCount => _coolDownCount;

    public DatadogProfilerConfiguration WithScenario(string scenario, bool enabled)
    {
        _enabledScenarios ??= new();
        _enabledScenarios[scenario] = enabled;
        return this;
    }

    public DatadogProfilerConfiguration RunAtCoolDown(bool coolDownPhase = true)
    {
        _runAtCoolDown = coolDownPhase;
        return this;
    }

    public DatadogProfilerConfiguration WithCoolDownCount(int coolDownCount)
    {
        _coolDownCount = coolDownCount;
        return this;
    }

    internal IReadOnlyDictionary<string, bool>? GetEnabledScenarios() => _enabledScenarios;
}
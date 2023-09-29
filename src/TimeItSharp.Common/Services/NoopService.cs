using TimeItSharp.Common.Configuration;

namespace TimeItSharp.Common.Services;

public sealed class NoopService : IService
{
    public string Name => nameof(NoopService);

    public void Initialize(Config configuration, TimeItCallbacks callbacks)
    {
    }

    public object? GetExecutionServiceData() => null;

    public object? GetScenarioServiceData() => null;
}
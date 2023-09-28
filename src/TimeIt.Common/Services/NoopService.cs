using TimeIt.Common.Configuration;

namespace TimeIt.Common.Services;

public sealed class NoopService : IService
{
    public string Name => nameof(NoopService);

    public void Initialize(Config configuration, TimeItCallbacks callbacks)
    {
    }

    public object? GetExecutionServiceData() => null;

    public object? GetScenarioServiceData() => null;
}
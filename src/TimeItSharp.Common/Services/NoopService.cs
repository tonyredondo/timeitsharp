namespace TimeItSharp.Common.Services;

public sealed class NoopService : IService
{
    public string Name => nameof(NoopService);

    public void Initialize(InitOptions options, TimeItCallbacks callbacks)
    {
    }

    public object? GetExecutionServiceData() => null;

    public object? GetScenarioServiceData() => null;
}
namespace TimeItSharp.Common.Services;

public interface IService : INamedExtension
{
    void Initialize(InitOptions options, TimeItCallbacks callbacks);

    object? GetExecutionServiceData();

    object? GetScenarioServiceData();
}
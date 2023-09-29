using TimeItSharp.Common.Configuration;

namespace TimeItSharp.Common.Services;

public interface IService : INamedExtension
{
    void Initialize(Config configuration, TimeItCallbacks callbacks);

    object? GetExecutionServiceData();

    object? GetScenarioServiceData();
}
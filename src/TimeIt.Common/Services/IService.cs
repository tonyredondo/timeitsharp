using TimeIt.Common.Configuration;

namespace TimeIt.Common.Services;

public interface IService
{
    string Name { get; }

    void Initialize(Config configuration, TimeItCallbacks callbacks);

    object? GetExecutionServiceData();

    object? GetScenarioServiceData();
}

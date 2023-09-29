using TimeItSharp.Common.Services;

namespace TimeItSharp.Common.Assertors;

public readonly struct AssertionData
{
    public readonly int ScenarioId;
    public readonly string ScenarioName;
    public readonly DateTime Start;
    public readonly DateTime End;
    public readonly TimeSpan Duration;
    public readonly int ExitCode;
    public readonly string StandardOutput;
    public readonly string StandardError;
    public readonly IReadOnlyList<IService> Services;

    public AssertionData(int scenarioId, string scenarioName, DateTime start, DateTime end, TimeSpan duration, int exitCode, string standardOutput,
        string standardError, IReadOnlyList<IService> services)
    {
        ScenarioId = scenarioId;
        ScenarioName = scenarioName;
        Start = start;
        End = end;
        Duration = duration;
        ExitCode = exitCode;
        StandardOutput = standardOutput;
        StandardError = standardError;
        Services = services;
    }

    public T? GetService<T>()
    {
        if (Services is null || Services.Count == 0)
        {
            return default;
        }

        foreach (var service in Services)
        {
            if (service is T sT)
            {
                return sT;
            }
        }

        return default;
    }
}
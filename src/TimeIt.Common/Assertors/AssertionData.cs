namespace TimeIt.Common.Assertors;

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

    public AssertionData(int scenarioId, string scenarioName, DateTime start, DateTime end, TimeSpan duration, int exitCode, string standardOutput,
        string standardError)
    {
        ScenarioId = scenarioId;
        ScenarioName = scenarioName;
        Start = start;
        End = end;
        Duration = duration;
        ExitCode = exitCode;
        StandardOutput = standardOutput;
        StandardError = standardError;
    }
}
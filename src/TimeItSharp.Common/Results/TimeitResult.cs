namespace TimeItSharp.Common.Results;

public sealed class TimeitResult
{
    public IReadOnlyList<ScenarioResult> Scenarios { get; set; } = Array.Empty<ScenarioResult>();
    
    public double[][]? Overheads { get; set; }
}
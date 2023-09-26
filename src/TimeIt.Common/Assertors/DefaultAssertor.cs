using System.Text;
using TimeIt.Common.Results;
using TimeIt.Common.Services;

namespace TimeIt.Common.Assertors;

public sealed class DefaultAssertor : Assertor
{
    private readonly StringBuilder _sbuilder = new(2500);
    private int _currentScenario = -1;
    private int _consecutiveErrorCount = 0;

    public override AssertResponse ScenarioAssertion(IReadOnlyList<DataPoint> dataPoints)
    {
        // if 10% of the datapoints failed then we set the scenario as a failure.
        var maxErrorsUntilFailure = dataPoints.Count / 10; 
        var errorsHashSet = new HashSet<string>();
        var status = Status.Passed;
        var numOfErrors = 0;
        foreach (var dataPoint in dataPoints)
        {
            if (!string.IsNullOrEmpty(dataPoint.AssertResults.Message))
            {
                errorsHashSet.Add(dataPoint.AssertResults.Message);
                if (dataPoint.Status == Status.Failed)
                {
                    if (++numOfErrors >= maxErrorsUntilFailure)
                    {
                        status = Status.Failed;
                    }
                }
            }
        }

        var message = string.Empty;
        if (errorsHashSet.Count > 0)
        {
            message = string.Join(Environment.NewLine, errorsHashSet);
        }

        return new AssertResponse(status, message);
    }

    public override AssertResponse ExecutionAssertion(in AssertionData data)
    {
        if (_currentScenario != data.ScenarioId)
        {
            _currentScenario = data.ScenarioId;
            _consecutiveErrorCount = 0;
        }

        if (data.ExitCode != 0)
        {
            _sbuilder.AppendLine($"ExitCode: {data.ExitCode}");
            _sbuilder.AppendLine("Standard Error: ");
            _sbuilder.AppendLine(data.StandardError);
            _sbuilder.AppendLine("Standard Output: ");
            _sbuilder.AppendLine(data.StandardOutput);
            var message = _sbuilder.ToString();
            _sbuilder.Clear();
            _consecutiveErrorCount++;
            return new AssertResponse(
                status: Status.Failed,
                shouldContinue: _consecutiveErrorCount < 5,
                message: message);
        }

        _consecutiveErrorCount = 0;
        return new AssertResponse(Status.Passed);
    }
}
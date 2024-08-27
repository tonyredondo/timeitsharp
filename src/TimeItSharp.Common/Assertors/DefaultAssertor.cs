using System.Text;
using TimeItSharp.Common.Results;

namespace TimeItSharp.Common.Assertors;

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
            var stdErr = data.StandardError ?? string.Empty;
            if (stdErr.Length > 1024)
            {
                stdErr = "..." + stdErr[^1024..];
            }

            _sbuilder.AppendLine(string.IsNullOrEmpty(stdErr) ? "<null>" : stdErr);

            _sbuilder.AppendLine("Standard Output: ");
            var stdOut = data.StandardOutput ?? string.Empty;
            if (stdOut.Length > 512)
            {
                stdOut = "..." + stdOut[^512..];
            }

            _sbuilder.AppendLine(string.IsNullOrEmpty(stdOut) ? "<null>" : stdOut);
            var message = _sbuilder.ToString();
            _sbuilder.Clear();
            _consecutiveErrorCount++;

            if (Options.Configuration.ProcessFailedDataPoints)
            {
                return new AssertResponse(
                    status: Status.Failed,
                    shouldContinue: true,
                    message: message);
            }
            else
            {
                return new AssertResponse(
                    status: Status.Failed,
                    shouldContinue: _consecutiveErrorCount < 5,
                    message: message);
            }
        }

        _consecutiveErrorCount = 0;
        return new AssertResponse(Status.Passed);
    }
}
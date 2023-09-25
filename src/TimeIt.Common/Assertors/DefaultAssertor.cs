using System.Text;
using TimeIt.Common.Results;

namespace TimeIt.Common.Assertors;

public class DefaultAssertor : Assertor
{
    private readonly StringBuilder _sbuilder = new(2500);
    private int _currentScenario = -1;
    private int _consecutiveErrorCount = 0;

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
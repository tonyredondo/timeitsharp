using System.Text;
using TimeItSharp.Common.Results;

namespace TimeItSharp.Common.Assertors;

public sealed class DefaultAssertor : Assertor
{
    private readonly StringBuilder _sbuilder = new(2500);
    private int _currentScenario = -1;
    private int _consecutiveErrorCount = 0;
    
    private ScenarioResult? _baseLineScenarioResult;

    public override AssertResponse ScenarioAssertion(ScenarioResult scenarioResult)
    {
        // if 10% of the datapoints failed then we set the scenario as a failure.
        var maxErrorsUntilFailure = scenarioResult.Count / 10;
        var errorReason = string.Empty;
        var errorsHashSet = new HashSet<string>();
        var status = Status.Passed;
        var numOfErrors = 0;
        foreach (var dataPoint in scenarioResult.Data)
        {
            if (!string.IsNullOrEmpty(dataPoint.AssertResults.Message))
            {
                errorsHashSet.Add(dataPoint.AssertResults.Message);
                if (dataPoint.Status == Status.Failed)
                {
                    if (++numOfErrors >= maxErrorsUntilFailure)
                    {
                        errorReason = $"Reason: 10% of the data points failed (errors count: {numOfErrors}).";
                        status = Status.Failed;
                    }
                }
            }
        }

        // If there are errors, we tag the scenario with the number of errors
        if (numOfErrors > 0)
        {
            // Tag the scenario with overhead and add it to additional metrics
            scenarioResult.Tags["test.number_of_errors"] = numOfErrors;
            scenarioResult.AdditionalMetrics["NumberOfErrors"] = numOfErrors;
        }

        // Check for overhead failures
        if (scenarioResult.Scenario is { } currentScenario && Options.Configuration.OverheadThreshold > 0.0d)
        {
            var overheadThreshold = Math.Min(1.0d, Options.Configuration.OverheadThreshold);
            if (currentScenario.IsBaseline)
            {
                _baseLineScenarioResult = scenarioResult;
                scenarioResult.AdditionalMetrics["Overhead%"] = 0.0d;
            }
            else if (_baseLineScenarioResult != null)
            {
                var currentMean = scenarioResult.Mean;
                var baselineMean = _baseLineScenarioResult.Mean;
                var overhead = (currentMean - baselineMean) / baselineMean;
                var orverheadInPercent = Math.Round(overhead * 100.0d, 2);

                // Tag the scenario with overhead and add it to additional metrics
                scenarioResult.Tags["test.overhead"] = orverheadInPercent;
                scenarioResult.AdditionalMetrics["Overhead%"] = orverheadInPercent;

                if (scenarioResult.Status == Status.Passed && overhead > overheadThreshold)
                {
                    currentMean = Utils.FromNanosecondsToMilliseconds(currentMean);
                    baselineMean = Utils.FromNanosecondsToMilliseconds(baselineMean);
                    errorReason =
                        $"Overhead threshold exceeded: {overhead:P2} (current: {currentMean}ms, baseline: {baselineMean}ms)";
                    status = Status.Failed;
                }
            }
        }

        var message = errorReason;
        if (errorsHashSet.Count > 0)
        {
            message = errorReason + Environment.NewLine + string.Join(Environment.NewLine, errorsHashSet);
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

            if (Options.Configuration.ProcessFailedDataPoints || Options.Configuration.DebugMode)
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
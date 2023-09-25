using TimeIt.Common.Configuration;
using TimeIt.Common.Results;

namespace TimeIt.Common.Assertors;

public interface IAssertor
{
    string Name { get; }

    bool Enabled { get; }

    void SetConfiguration(Config configuration);

    AssertResponse ScenarioAssertion(IReadOnlyList<DataPoint> dataPoints);
    
    AssertResponse ExecutionAssertion(in AssertionData data);
}
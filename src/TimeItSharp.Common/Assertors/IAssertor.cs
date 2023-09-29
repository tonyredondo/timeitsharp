using TimeItSharp.Common.Configuration;
using TimeItSharp.Common.Results;

namespace TimeItSharp.Common.Assertors;

public interface IAssertor : INamedExtension
{
    bool Enabled { get; }

    void SetConfiguration(Config configuration);

    AssertResponse ScenarioAssertion(IReadOnlyList<DataPoint> dataPoints);
    
    AssertResponse ExecutionAssertion(in AssertionData data);
}
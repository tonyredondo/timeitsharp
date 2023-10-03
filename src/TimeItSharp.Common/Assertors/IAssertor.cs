using TimeItSharp.Common.Results;

namespace TimeItSharp.Common.Assertors;

public interface IAssertor : INamedExtension
{
    bool Enabled { get; }

    void Initialize(InitOptions options);

    AssertResponse ScenarioAssertion(IReadOnlyList<DataPoint> dataPoints);
    
    AssertResponse ExecutionAssertion(in AssertionData data);
}
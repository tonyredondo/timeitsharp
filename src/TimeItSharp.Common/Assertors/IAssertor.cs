using TimeItSharp.Common.Results;

namespace TimeItSharp.Common.Assertors;

public interface IAssertor : INamedExtension
{
    bool Enabled { get; }

    void Initialize(InitOptions options);

    AssertResponse ScenarioAssertion(ScenarioResult scenarioResult);
    
    AssertResponse ExecutionAssertion(in AssertionData data);
}
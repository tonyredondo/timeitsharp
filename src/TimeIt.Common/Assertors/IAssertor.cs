using TimeIt.Common.Configuration;

namespace TimeIt.Common.Assertors;

public interface IAssertor
{
    string Name { get; }

    bool Enabled { get; }

    void SetConfiguration(Config configuration);

    AssertResponse ExecutionAssertion(in AssertionData data);
}
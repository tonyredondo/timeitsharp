using TimeItSharp.Common.Results;

namespace TimeItSharp.Common.Assertors;

public abstract class Assertor : IAssertor
{
    public virtual string Name { get; }
    public virtual bool Enabled { get; }
    
    protected InitOptions Options { get; private set; }

    public Assertor()
    {
        Name = this.GetType().Name;
        Enabled = true;
    }

    public void Initialize(InitOptions options)
    {
        Options = options;
        OnInitialize(options);
    }

    protected virtual void OnInitialize(InitOptions options)
    {
    }

    public abstract AssertResponse ScenarioAssertion(IReadOnlyList<DataPoint> dataPoints);

    public abstract AssertResponse ExecutionAssertion(in AssertionData data);
}
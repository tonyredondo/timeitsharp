using TimeIt.Common.Configuration;

namespace TimeIt.Common.Assertors;

public abstract class Assertor : IAssertor
{
    public virtual string Name { get; }
    public virtual bool Enabled { get; }

    public Assertor()
    {
        Name = this.GetType().Name;
        Enabled = true;
    }

    public virtual void SetConfiguration(Config configuration)
    {
    }

    public abstract AssertResponse ExecutionAssertion(in AssertionData data);
}
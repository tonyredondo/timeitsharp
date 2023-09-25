using TimeIt.Common.Results;

namespace TimeIt.Common.Assertors;

public class DefaultAssertor : Assertor
{
    public override AssertResponse ExecutionAssertion(in AssertionData data)
    {
        if (data.ExitCode != 0)
        {
            return new AssertResponse(Status.Failed, true,
                data.StandardError + Environment.NewLine + data.StandardOutput);
        }

        return new AssertResponse(Status.Passed);
    }
}
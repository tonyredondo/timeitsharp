using TimeItSharp.Common.Results;

namespace TimeItSharp.Common.Assertors;

public readonly struct AssertResponse
{
    public readonly Status Status;
    public readonly bool ShouldContinue;
    public readonly string Message;

    public AssertResponse(Status status, bool shouldContinue, string message)
    {
        Status = status;
        ShouldContinue = shouldContinue;
        Message = message;
    }

    public AssertResponse(Status status, bool shouldContinue)
    {
        Status = status;
        ShouldContinue = shouldContinue;
        Message = string.Empty;
    }

    public AssertResponse(Status status)
    {
        Status = status;
        ShouldContinue = true;
        Message = string.Empty;
    }

    public AssertResponse(Status status, string message)
    {
        Status = status;
        ShouldContinue = true;
        Message = message;
    }
}
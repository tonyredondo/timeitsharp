using TimeItSharp.Common.Configuration;
using TimeItSharp.Common.Services;

namespace TimeItSharp.Tests;

public sealed class TimeItCallbacksTests
{
    [Fact]
    public void RepeatScenarioForService_rejects_null_service()
    {
        var callbacks = new TimeItCallbacks();
        callbacks.OnScenarioStart += argument => argument.RepeatScenarioForService(null!, 1);
        var triggers = callbacks.GetTriggers();

        Assert.Throws<ArgumentNullException>(() =>
            triggers.ScenarioStart(new TimeItCallbacks.ScenarioStartArg(new Scenario("scenario", false))));
    }

    [Fact]
    public void RepeatScenarioForService_rejects_non_positive_count()
    {
        var callbacks = new TimeItCallbacks();
        callbacks.OnScenarioStart += argument => argument.RepeatScenarioForService(new NoopService(), 0);
        var triggers = callbacks.GetTriggers();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            triggers.ScenarioStart(new TimeItCallbacks.ScenarioStartArg(new Scenario("scenario", false))));
    }
}

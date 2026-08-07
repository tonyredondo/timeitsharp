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
    [Fact]
    public void Concurrent_subscriptions_are_not_lost()
    {
        var callbacks = new TimeItCallbacks();
        const int callbackCount = 2_000;
        var calls = 0;
        var subscriptions = Enumerable.Range(0, callbackCount)
            .Select(_ => (TimeItCallbacks.OnFinishDelegate)(() => Interlocked.Increment(ref calls)))
            .ToArray();

        Parallel.ForEach(subscriptions, callback => callbacks.OnFinish += callback);

        callbacks.GetTriggers().Finish();
        Assert.Equal(callbackCount, calls);
    }

    [Fact]
    public void Concurrent_removals_are_not_lost()
    {
        var callbacks = new TimeItCallbacks();
        const int callbackCount = 2_000;
        var calls = 0;
        var subscriptions = Enumerable.Range(0, callbackCount)
            .Select(_ => (TimeItCallbacks.OnFinishDelegate)(() => Interlocked.Increment(ref calls)))
            .ToArray();
        foreach (var callback in subscriptions)
        {
            callbacks.OnFinish += callback;
        }

        Parallel.ForEach(subscriptions, callback => callbacks.OnFinish -= callback);

        callbacks.GetTriggers().Finish();
        Assert.Equal(0, calls);
    }

}

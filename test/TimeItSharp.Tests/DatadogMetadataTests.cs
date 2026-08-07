namespace TimeItSharp.Tests;

public sealed class DatadogMetadataTests
{
    [Fact]
    public void Release_is_null_safe_idempotent_and_recreates_metadata()
    {
        var key = new object();

        try
        {
            TimeItSharp.Common.DatadogMetadata.GetIds(key, out _, out var firstSpanId);
            TimeItSharp.Common.DatadogMetadata.GetIds(key, out _, out var sameSpanId);

            Assert.Equal(firstSpanId, sameSpanId);

            TimeItSharp.Common.DatadogMetadata.Release(null);
            TimeItSharp.Common.DatadogMetadata.Release(key);
            TimeItSharp.Common.DatadogMetadata.Release(key);

            TimeItSharp.Common.DatadogMetadata.GetIds(key, out _, out var recreatedSpanId);
            Assert.NotEqual(firstSpanId, recreatedSpanId);
        }
        finally
        {
            TimeItSharp.Common.DatadogMetadata.Release(key);
        }
    }
    [Fact]
    public async Task Non_datadog_run_release_does_not_initialize_ci_visibility()
    {
        var initializationCount = TimeItSharp.Common.DatadogMetadata.InitializationCount;
        var config = new TimeItSharp.Common.Configuration.Config
        {
            Count = 1,
            EnableMetrics = false,
            EnableDatadog = false,
            ProcessName = "echo",
        };
        config.Scenarios.Add(new TimeItSharp.Common.Configuration.Scenario { Name = "no-datadog" });

        var exitCode = await TimeItSharp.Common.TimeItEngine.RunAsync(config);

        Assert.Equal(0, exitCode);
        Assert.Equal(initializationCount, TimeItSharp.Common.DatadogMetadata.InitializationCount);
    }

}

namespace TimeIt;

#if !NOCONSTANTS
public static class Constants
{
    public const string StartupHookEnvironmentVariable = "DOTNET_STARTUP_HOOKS";
    public const string TimeItMetricsTemporalPathEnvironmentVariable = "TIMEIT_METRICS_TEMPORAL_PATH";
    public const string ProcessTimeToStartMetricName = "process.time_to_start_ms";
    public const string ProcessTimeToEndMetricName = "process.time_to_end_ms";
    public const string ProcessInternalDurationMetricName = "process.internal_duration_ms";
    public const string ProcessStartTimeUtcMetricName = "process.start";
    public const string ProcessEndTimeUtcMetricName = "process.end";
}
#endif
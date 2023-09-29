namespace TimeItSharp;

internal static class Constants
{
    public const string StartupHookEnvironmentVariable = "DOTNET_STARTUP_HOOKS";
    public const string TimeItMetricsTemporalPathEnvironmentVariable = "TIMEIT_METRICS_TEMPORAL_PATH";
    public const string ProcessTimeToStartMetricName = "process.time_to_start_ms";
    public const string ProcessTimeToMainMetricName = "process.time_to_main_ms"; 
    public const string ProcessTimeToEndMetricName = "process.time_to_end_ms";
    public const string ProcessTimeToMainEndMetricName = "process.time_to_end_main_ms";
    public const string ProcessInternalDurationMetricName = "process.internal_duration_ms";
    public const string ProcessCorrectedDurationMetricName = "process.corrected_duration_ms";
    public const string ProcessStartupHookOverheadMetricName = "process.startuphook_overhead_ms";
    public const string ProcessStartTimeUtcMetricName = "process.start";
    public const string MainMethodStartTimeUtcMetricName = "main.start";
    public const string MainMethodEndTimeUtcMetricName = "main.end";
    public const string ProcessEndTimeUtcMetricName = "process.end";
}

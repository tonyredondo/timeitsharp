namespace TimeItSharp;

internal static class Constants
{
    public const string StartupHookEnvironmentVariable = "DOTNET_STARTUP_HOOKS";
    public const string TimeItMetricsTemporalPathEnvironmentVariable = "TIMEIT_METRICS_TEMPORAL_PATH";
    public const string TimeItMetricsProcessName = "TIMEIT_METRICS_PROCESS_NAME";
    
    public const string ProcessTimeToStartMetricNameString = "process.time_to_start_ms";
    public const string ProcessTimeToMainMetricNameString = "process.time_to_main_ms"; 
    public const string ProcessTimeToEndMetricNameString = "process.time_to_end_ms";
    public const string ProcessTimeToMainEndMetricNameString = "process.time_to_end_main_ms";
    public const string ProcessInternalDurationMetricNameString = "process.internal_duration_ms";
    public const string ProcessCorrectedDurationMetricNameString = "process.corrected_duration_ms";
    public const string ProcessStartupHookOverheadMetricNameString = "process.startuphook_overhead_ms";
    public const string ProcessStartTimeUtcMetricNameString = "process.start";
    public const string MainMethodStartTimeUtcMetricNameString = "main.start";
    public const string MainMethodEndTimeUtcMetricNameString = "main.end";
    public const string ProcessEndTimeUtcMetricNameString = "process.end";

    public static ReadOnlySpan<byte> ProcessTimeToStartMetricName => "process.time_to_start_ms"u8;
    public static ReadOnlySpan<byte> ProcessTimeToMainMetricName => "process.time_to_main_ms"u8; 
    public static ReadOnlySpan<byte> ProcessTimeToEndMetricName => "process.time_to_end_ms"u8;
    public static ReadOnlySpan<byte> ProcessTimeToMainEndMetricName => "process.time_to_end_main_ms"u8;
    public static ReadOnlySpan<byte> ProcessInternalDurationMetricName => "process.internal_duration_ms"u8;
    public static ReadOnlySpan<byte> ProcessCorrectedDurationMetricName => "process.corrected_duration_ms"u8;
    public static ReadOnlySpan<byte> ProcessStartupHookOverheadMetricName => "process.startuphook_overhead_ms"u8;
    public static ReadOnlySpan<byte> ProcessStartTimeUtcMetricName => "process.start"u8;
    public static ReadOnlySpan<byte> MainMethodStartTimeUtcMetricName => "main.start"u8;
    public static ReadOnlySpan<byte> MainMethodEndTimeUtcMetricName => "main.end"u8;
    public static ReadOnlySpan<byte> ProcessEndTimeUtcMetricName => "process.end"u8;
}

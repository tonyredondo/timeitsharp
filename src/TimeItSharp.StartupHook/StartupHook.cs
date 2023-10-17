using System.Diagnostics;
using TimeItSharp;
using TimeItSharp.RuntimeMetrics;

public sealed class StartupHook
{
    private static RuntimeMetricsWriter? _metricsWriter;
    private static FileStorage? _fileStatsd;

    public static void Initialize()
    {
        var startDate = Clock.UtcNow;
        var metricsPath = Environment.GetEnvironmentVariable(Constants.TimeItMetricsTemporalPathEnvironmentVariable);
        if (string.IsNullOrEmpty(metricsPath))
        {
            return;
        }

        bool enableMetrics;
        var processName = Environment.GetEnvironmentVariable(Constants.TimeItMetricsProcessName);
        if (string.IsNullOrEmpty(processName))
        {
            enableMetrics = true;
        }
        else
        {
            var currentProcessName = Process.GetCurrentProcess().ProcessName;
            enableMetrics = string.Equals(currentProcessName, processName, StringComparison.OrdinalIgnoreCase);
        }

        if (enableMetrics)
        {
            _fileStatsd = new FileStorage(metricsPath);
            _fileStatsd.Gauge(Constants.ProcessStartTimeUtcMetricName, startDate.ToBinary());
            _metricsWriter = new RuntimeMetricsWriter(_fileStatsd, TimeSpan.FromMilliseconds(50));
            _metricsWriter.PushEvents();
            AppDomain.CurrentDomain.ProcessExit += CurrentDomainOnProcessExit;
            _fileStatsd.Gauge(Constants.MainMethodStartTimeUtcMetricName, Clock.UtcNow.ToBinary());
        }
    }

    private static void CurrentDomainOnProcessExit(object? sender, EventArgs e)
    {
        if (_fileStatsd is { } fileStatsd)
        {
            fileStatsd.Gauge(Constants.MainMethodEndTimeUtcMetricName, Clock.UtcNow.ToBinary());
            _metricsWriter?.PushEvents();
            fileStatsd.Gauge(Constants.ProcessEndTimeUtcMetricName, Clock.UtcNow.ToBinary());
            fileStatsd.Dispose();
        }
    }
}
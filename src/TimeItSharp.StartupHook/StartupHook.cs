using TimeItSharp;
using TimeItSharp.RuntimeMetrics;

public sealed class StartupHook
{
    private static RuntimeMetricsWriter? _metricsWriter;
    private static FileStorage? _fileStatsd;

    public static void Initialize()
    {
        var startDate = Clock.UtcNow;
        if (Environment.GetEnvironmentVariable(Constants.TimeItMetricsTemporalPathEnvironmentVariable) is
            { Length: > 0 } metricsPath)
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
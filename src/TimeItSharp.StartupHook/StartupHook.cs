using TimeItSharp;
using TimeItSharp.RuntimeMetrics;

public sealed class StartupHook
{
    private static RuntimeMetricsWriter? _metricsWriter;
    private static BinaryFileStorage? _fileStatsd;

    public static void Initialize()
    {
        var startDate = Clock.UtcNow;
        var metricsPath = Environment.GetEnvironmentVariable(Constants.TimeItMetricsTemporalPathEnvironmentVariable);
        if (metricsPath == null || metricsPath.Length == 0)
        {
            return;
        }

        bool enableMetrics;
        var processName = Environment.GetEnvironmentVariable(Constants.TimeItMetricsProcessName);
        if (processName == null || processName.Length == 0)
        {
            enableMetrics = true;
        }
        else
        {
            var currentProcessName = ProcessHelpers.ProcessName;
            if (processName.IndexOf(';') == -1)
            {
                enableMetrics = string.Equals(currentProcessName, processName, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                enableMetrics = processName.Split(';').Any(pName =>
                    string.Equals(currentProcessName, pName, StringComparison.OrdinalIgnoreCase));
            }
        }

        if (enableMetrics)
        {
            _fileStatsd = new BinaryFileStorage(metricsPath);
            _fileStatsd.Gauge(Constants.ProcessStartTimeUtcMetricName, startDate.ToBinary());
            _metricsWriter = new RuntimeMetricsWriter(_fileStatsd, TimeSpan.FromMilliseconds(100));
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
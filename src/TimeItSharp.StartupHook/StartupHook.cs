using TimeItSharp;
using TimeItSharp.RuntimeMetrics;

public sealed class StartupHook
{
    private static RuntimeMetricsWriter? _metricsWriter;
    private static DateTime _startTime;
    private static DateTime _mainMethodStartTime;

    public static void Initialize()
    {
        _startTime = Clock.UtcNow;
        if (Environment.GetEnvironmentVariable(Constants.TimeItMetricsTemporalPathEnvironmentVariable) is not { Length: > 0 } metricsPath)
        {
            return;
        }

        var enableMetrics = true;
        if (Environment.GetEnvironmentVariable(Constants.TimeItMetricsProcessName) is { Length: > 0 } processName)
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

        if (!enableMetrics)
        {
            return;
        }

        var frequencyInMs = 200;
        if (Environment.GetEnvironmentVariable(Constants.TimeItMetricsFrequency) is { Length: > 0 } frequency)
        {
            if (frequency == "100")
            {
                frequencyInMs = 100;
            }
            else if (frequency == "300")
            {
                frequencyInMs = 300;
            }
            else
            {
                frequencyInMs = int.Parse(frequency);
            }
        }
        
        _metricsWriter = new RuntimeMetricsWriter(new BinaryFileStorage(metricsPath), TimeSpan.FromMilliseconds(frequencyInMs));
        _metricsWriter.PushEvents();
        AppDomain.CurrentDomain.ProcessExit += CurrentDomainOnProcessExit;
        _mainMethodStartTime = Clock.UtcNow;
    }

    private static void CurrentDomainOnProcessExit(object? sender, EventArgs e)
    {
        if (_metricsWriter is null)
        {
            return;
        }

        var mp1 = new BinaryFileStorage.MetricPayload(BinaryFileStorage.MetricType.Gauge,
            Constants.ProcessStartTimeUtcMetricName, _startTime.ToBinary());
        var mp2 = new BinaryFileStorage.MetricPayload(BinaryFileStorage.MetricType.Gauge,
            Constants.MainMethodStartTimeUtcMetricName, _mainMethodStartTime.ToBinary());
        var mp3 = new BinaryFileStorage.MetricPayload(BinaryFileStorage.MetricType.Gauge,
            Constants.MainMethodEndTimeUtcMetricName, Clock.UtcNow.ToBinary());
        _metricsWriter.Storage.WritePayload(in mp1, in mp2, in mp3);

        _metricsWriter.PushEvents();

        var mp4 = new BinaryFileStorage.MetricPayload(BinaryFileStorage.MetricType.Gauge,
            Constants.ProcessEndTimeUtcMetricName, Clock.UtcNow.ToBinary());
        _metricsWriter.Storage.WritePayload(in mp4);
        _metricsWriter.Storage.Dispose();
    }
}
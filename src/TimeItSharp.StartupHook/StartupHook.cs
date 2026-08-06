using TimeItSharp;
using TimeItSharp.RuntimeMetrics;

public sealed class StartupHook
{
    private static readonly object SyncRoot = new();
    private static RuntimeMetricsWriter? _metricsWriter;
    private static DateTime _startTime;
    private static DateTime _mainMethodStartTime;
    private static bool _shuttingDown;

    public static void Initialize()
    {
        lock (SyncRoot)
        {
            // Startup hooks can be initialized more than once by a host. Keep one writer and one
            // ProcessExit subscription rather than leaking timers and duplicate metrics.
            if (_metricsWriter is not null || _shuttingDown)
            {
                return;
            }

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

            const int defaultFrequencyInMs = 200;
            var frequencyInMs = defaultFrequencyInMs;
            if (Environment.GetEnvironmentVariable(Constants.TimeItMetricsFrequency) is { Length: > 0 } frequency &&
                int.TryParse(frequency, out var parsedFrequencyInMs) &&
                parsedFrequencyInMs > 0)
            {
                frequencyInMs = parsedFrequencyInMs;
            }

            _metricsWriter = new RuntimeMetricsWriter(new BinaryFileStorage(metricsPath), TimeSpan.FromMilliseconds(frequencyInMs));
            _metricsWriter.PushEvents();
            AppDomain.CurrentDomain.ProcessExit += CurrentDomainOnProcessExit;
            _mainMethodStartTime = Clock.UtcNow;
        }
    }

    private static void CurrentDomainOnProcessExit(object? sender, EventArgs e)
    {
        RuntimeMetricsWriter? metricsWriter;
        lock (SyncRoot)
        {
            if (_shuttingDown)
            {
                return;
            }

            _shuttingDown = true;
            metricsWriter = _metricsWriter;
            _metricsWriter = null;
        }

        if (metricsWriter is null)
        {
            return;
        }

        try
        {
            var mp1 = new BinaryFileStorage.MetricPayload(BinaryFileStorage.MetricType.Gauge,
                Constants.ProcessStartTimeUtcMetricName, _startTime.ToBinary());
            var mp2 = new BinaryFileStorage.MetricPayload(BinaryFileStorage.MetricType.Gauge,
                Constants.MainMethodStartTimeUtcMetricName, _mainMethodStartTime.ToBinary());
            var mp3 = new BinaryFileStorage.MetricPayload(BinaryFileStorage.MetricType.Gauge,
                Constants.MainMethodEndTimeUtcMetricName, Clock.UtcNow.ToBinary());
            metricsWriter.Storage.WritePayload(in mp1, in mp2, in mp3);

            metricsWriter.PushEvents();

            var mp4 = new BinaryFileStorage.MetricPayload(BinaryFileStorage.MetricType.Gauge,
                Constants.ProcessEndTimeUtcMetricName, Clock.UtcNow.ToBinary());
            metricsWriter.Storage.WritePayload(in mp4);
        }
        catch
        {
            // Metrics must never interfere with process shutdown.
        }
        finally
        {
            try
            {
                metricsWriter.Dispose();
            }
            catch
            {
                // Metrics cleanup must not interfere with process shutdown.
            }

            try
            {
                metricsWriter.Storage.Dispose();
            }
            catch
            {
                // Metrics cleanup must not interfere with process shutdown.
            }
        }
    }
}

using System.Runtime.ExceptionServices;

#nullable disable

namespace TimeItSharp.RuntimeMetrics;

internal sealed class RuntimeMetricsWriter : IDisposable
{
    private readonly TimeSpan _delay;
    private readonly double _maximumCpu;
    private readonly BinaryFileStorage _storage;
    private readonly Timer _timer;
    private readonly RuntimeEventListener _listener;
    private readonly bool _enableProcessMetrics;

    private TimeSpan _previousUserCpu;
    private TimeSpan _previousSystemCpu;
    private TimeSpan _previousTotalCpu;
    private int _exceptionCounts;

    internal RuntimeMetricsWriter(BinaryFileStorage storage, TimeSpan delay)
    {
        _delay = delay;
        _maximumCpu = Environment.ProcessorCount * _delay.TotalMilliseconds;
        _storage = storage;
        _timer = new Timer(_ => PushEvents(), null, delay, delay);

        try
        {
            AppDomain.CurrentDomain.FirstChanceException += FirstChanceException;
        }
        catch
        {
            // .
        }

        try
        {
            ProcessHelpers.GetCurrentProcessRuntimeMetrics(out var totalCpu, out var userCpu, out var systemCpu, out _, out _);
            _previousUserCpu = userCpu;
            _previousSystemCpu = systemCpu;
            _previousTotalCpu = totalCpu;

            _enableProcessMetrics = true;
        }
        catch
        {
            _enableProcessMetrics = false;
        }

        try
        {
            _listener = new RuntimeEventListener(storage, delay);
        }
        catch
        {
            // .
        }
    }

    public BinaryFileStorage Storage => _storage;

    public void Dispose()
    {
        AppDomain.CurrentDomain.FirstChanceException -= FirstChanceException;
        _timer.Dispose();
        _listener?.Dispose();
    }

    internal void PushEvents()
    {
        try
        {
            _listener?.Refresh();

            if (!_enableProcessMetrics)
            {
                return;
            }

            ProcessHelpers.GetCurrentProcessRuntimeMetrics(out var newTotalCpu, out var newUserCpu, out var newSystemCpu,
                out var threadCount, out var memoryUsage);

            var userCpu = newUserCpu - _previousUserCpu;
            var systemCpu = newSystemCpu - _previousSystemCpu;
            var totalCpu = newTotalCpu - _previousTotalCpu;

            _previousUserCpu = newUserCpu;
            _previousSystemCpu = newSystemCpu;
            _previousTotalCpu = newTotalCpu;

            // Note: the behavior of Environment.ProcessorCount has changed a lot accross version: https://github.com/dotnet/runtime/issues/622
            // What we want is the number of cores attributed to the container, which is the behavior in 3.1.2+ (and, I believe, in 2.x)
                
            var totalSeconds = _delay.TotalSeconds;
            var exceptionCounts = Interlocked.Exchange(ref _exceptionCounts, 0);

            var mp1 = new BinaryFileStorage.MetricPayload(BinaryFileStorage.MetricType.Gauge, MetricsNames.ThreadsCount,
                threadCount);
            var mp2 = new BinaryFileStorage.MetricPayload(BinaryFileStorage.MetricType.Gauge,
                MetricsNames.CommittedMemory, memoryUsage);
            var mp3 = new BinaryFileStorage.MetricPayload(BinaryFileStorage.MetricType.Gauge, MetricsNames.PrivateBytes,
                memoryUsage);
            var mp4 = new BinaryFileStorage.MetricPayload(BinaryFileStorage.MetricType.Gauge, MetricsNames.CpuUserTime,
                userCpu.TotalMilliseconds / totalSeconds);
            var mp5 = new BinaryFileStorage.MetricPayload(BinaryFileStorage.MetricType.Gauge,
                MetricsNames.CpuSystemTime, systemCpu.TotalMilliseconds / totalSeconds);
            var mp6 = new BinaryFileStorage.MetricPayload(BinaryFileStorage.MetricType.Gauge,
                MetricsNames.ProcessorTime, totalCpu.TotalMilliseconds / totalSeconds);
            var mp7 = new BinaryFileStorage.MetricPayload(BinaryFileStorage.MetricType.Gauge,
                MetricsNames.CpuPercentage,
                Math.Round(totalCpu.TotalMilliseconds * 100 / _maximumCpu, 1, MidpointRounding.AwayFromZero));
            var mp8 = new BinaryFileStorage.MetricPayload(BinaryFileStorage.MetricType.Increment,
                MetricsNames.ExceptionsCount, exceptionCounts);
            _storage.WritePayload(in mp1, in mp2, in mp3, in mp4, in mp5, in mp6, in mp7, in mp8);
        }
        catch
        {
            // .
        }
    }

    private void FirstChanceException(object sender, FirstChanceExceptionEventArgs e)
    {
        Interlocked.Increment(ref _exceptionCounts);
    }
}
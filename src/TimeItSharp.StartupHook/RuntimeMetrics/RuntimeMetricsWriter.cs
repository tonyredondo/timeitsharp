using System.Runtime.ExceptionServices;

#nullable disable

namespace TimeItSharp.RuntimeMetrics;

internal sealed class RuntimeMetricsWriter : IDisposable
{
    private readonly TimeSpan _delay;
    private readonly FileStorage _storage;
    private readonly Timer _timer;
    private readonly RuntimeEventListener _listener;
    private readonly bool _enableProcessMetrics;

    private TimeSpan _previousUserCpu;
    private TimeSpan _previousSystemCpu;
    private TimeSpan _previousTotalCpu;
    private int _exceptionCounts;

    internal RuntimeMetricsWriter(FileStorage storage, TimeSpan delay)
    {
        _delay = delay;
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

            if (_enableProcessMetrics)
            {
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
                var maximumCpu = Environment.ProcessorCount * _delay.TotalMilliseconds;

                _storage.Gauge(MetricsNames.ThreadsCount, threadCount);

                _storage.Gauge(MetricsNames.CommittedMemory, memoryUsage);
                _storage.Gauge(MetricsNames.PrivateBytes, memoryUsage);

                // Get CPU time in milliseconds per second
                var totalSeconds = _delay.TotalSeconds;
                _storage.Gauge(MetricsNames.CpuUserTime, userCpu.TotalMilliseconds / totalSeconds);
                _storage.Gauge(MetricsNames.CpuSystemTime, systemCpu.TotalMilliseconds / totalSeconds);
                _storage.Gauge(MetricsNames.ProcessorTime, totalCpu.TotalMilliseconds / totalSeconds);

                _storage.Gauge(MetricsNames.CpuPercentage,
                    Math.Round(totalCpu.TotalMilliseconds * 100 / maximumCpu, 1, MidpointRounding.AwayFromZero));
            }

            var exceptionCounts = Interlocked.Exchange(ref _exceptionCounts, 0);
            if (exceptionCounts > 0)
            {
                _storage.Increment(MetricsNames.ExceptionsCount, exceptionCounts);
            }
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
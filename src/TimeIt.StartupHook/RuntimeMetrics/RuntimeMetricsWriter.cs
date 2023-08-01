using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;

#nullable disable

namespace TimeIt.RuntimeMetrics;

internal class RuntimeMetricsWriter : IDisposable
{
    private static readonly Func<FileStatsd, TimeSpan, RuntimeEventListener> InitializeListenerFunc = InitializeListener;

    private readonly TimeSpan _delay;
    private readonly FileStatsd _statsd;
    private readonly Timer _timer;
    private readonly RuntimeEventListener _listener;
    private readonly bool _enableProcessMetrics;
    private readonly ConcurrentDictionary<string, int> _exceptionCounts = new();

    private TimeSpan _previousUserCpu;
    private TimeSpan _previousSystemCpu;
    private TimeSpan _previousTotalCpu;

    public RuntimeMetricsWriter(FileStatsd statsd, TimeSpan delay)
        : this(statsd, delay, InitializeListenerFunc)
    {
    }

    internal RuntimeMetricsWriter(FileStatsd statsd, TimeSpan delay, Func<FileStatsd, TimeSpan, RuntimeEventListener> initializeListener)
    {
        _delay = delay;
        _statsd = statsd;
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
            _listener = initializeListener(statsd, delay);
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
        _exceptionCounts.Clear();
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

                _statsd.Gauge(MetricsNames.ThreadsCount, threadCount);

                _statsd.Gauge(MetricsNames.CommittedMemory, memoryUsage);
                _statsd.Gauge(MetricsNames.PrivateBytes, memoryUsage);

                // Get CPU time in milliseconds per second
                _statsd.Gauge(MetricsNames.CpuUserTime, userCpu.TotalMilliseconds / _delay.TotalSeconds);
                _statsd.Gauge(MetricsNames.CpuSystemTime, systemCpu.TotalMilliseconds / _delay.TotalSeconds);
                _statsd.Gauge(MetricsNames.ProcessorTime, totalCpu.TotalMilliseconds / _delay.TotalSeconds);

                _statsd.Gauge(MetricsNames.CpuPercentage,
                    Math.Round(totalCpu.TotalMilliseconds * 100 / maximumCpu, 1, MidpointRounding.AwayFromZero));
            }

            if (!_exceptionCounts.IsEmpty)
            {
                foreach (var element in _exceptionCounts)
                {
                    _statsd.Increment(MetricsNames.ExceptionsCount, element.Value,
                        tags: new[] { $"exception_type:{element.Key}" });
                }

                // There's a race condition where we could clear items that haven't been pushed
                // Having an exact exception count is probably not worth the overhead required to fix it
                _exceptionCounts.Clear();
            }
        }
        catch
        {
            // .
        }
    }

    private static RuntimeEventListener InitializeListener(FileStatsd statsd, TimeSpan delay)
    {
        return new RuntimeEventListener(statsd, delay);
    }

    private void FirstChanceException(object sender, FirstChanceExceptionEventArgs e)
    {
        var name = e.Exception.GetType().Name;
        _exceptionCounts.AddOrUpdate(name, 1, (_, count) => count + 1);
    }
}
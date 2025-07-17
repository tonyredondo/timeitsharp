using System.Collections.ObjectModel;
using System.Diagnostics.Tracing;

#nullable disable

namespace TimeItSharp.RuntimeMetrics;

// The following code is based on: https://github.com/DataDog/dd-trace-dotnet/blob/master/tracer/src/Datadog.Trace/RuntimeMetrics

internal sealed class RuntimeEventListener : EventListener
{
    private const string RuntimeEventSourceName = "Microsoft-Windows-DotNETRuntime";
    private const string AspNetCoreHostingEventSourceName = "Microsoft.AspNetCore.Hosting";
    private const string AspNetCoreKestrelEventSourceName = "Microsoft-AspNetCore-Server-Kestrel";
    private const int EventGcSuspendBegin = 9;
    private const int EventGcRestartEnd = 3;
    private const int EventGcHeapStats = 4;
    private const int EventContentionStop = 91;
    private const int EventGcGlobalHeapHistory = 205;

    private readonly BinaryFileStorage _storage;
    private readonly ReadOnlyDictionary<string, string> _eventCounterIntervalSecDictionary;

    private double _contentionTime;
    private long _contentionCount;
    private DateTime? _gcStart;

    public RuntimeEventListener(BinaryFileStorage storage, TimeSpan delay)
    {
        _storage = storage;
        _eventCounterIntervalSecDictionary = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
        {
            ["EventCounterIntervalSec"] = ((int)delay.TotalSeconds).ToString()
        });
        EventSourceCreated += (_, e) => EnableEventSource(e.EventSource);
    }

    public void Refresh()
    {
        // Can't use a Timing because Dogstatsd doesn't support local aggregation
        // It means that the aggregations in the UI would be wrong
        var mp1 = new BinaryFileStorage.MetricPayload(BinaryFileStorage.MetricType.Gauge, MetricsNames.ContentionTime,
            Interlocked.Exchange(ref _contentionTime, 0));
        var mp2 = new BinaryFileStorage.MetricPayload(BinaryFileStorage.MetricType.Counter,
            MetricsNames.ContentionCount, Interlocked.Exchange(ref _contentionCount, 0));
        var mp3 = new BinaryFileStorage.MetricPayload(BinaryFileStorage.MetricType.Gauge,
            MetricsNames.ThreadPoolWorkersCount, ThreadPool.ThreadCount);
        _storage.WritePayload(in mp1, in mp2, in mp3);
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        if (_storage == null)
        {
            // I know it sounds crazy at first, but because OnEventSourceCreated is called from the base constructor,
            // and EnableEvents is called from OnEventSourceCreated, it's entirely possible that OnEventWritten
            // gets called before the child constructor is called.
            // In that case, just bail out.
            return;
        }

        try
        {
            if (eventData.EventName == "EventCounters")
            {
                ExtractCounters(eventData.Payload);
            }
            else if (eventData.EventId == EventGcSuspendBegin)
            {
                _gcStart = eventData.TimeStamp;
            }
            else if (eventData.EventId == EventGcRestartEnd)
            {
                if (_gcStart is { } start)
                {
                    var mp = new BinaryFileStorage.MetricPayload(BinaryFileStorage.MetricType.Timer,
                        MetricsNames.GcPauseTime, (eventData.TimeStamp - start).TotalMilliseconds);
                    _storage.WritePayload(in mp);
                }
            }
            else
            {
                if (eventData.EventId == EventGcHeapStats)
                {
                    var stats = HeapStats.FromPayload(eventData.Payload);
                    var mp1 = new BinaryFileStorage.MetricPayload(BinaryFileStorage.MetricType.Gauge,
                        MetricsNames.Gen0HeapSize, stats.Gen0Size);
                    var mp2 = new BinaryFileStorage.MetricPayload(BinaryFileStorage.MetricType.Gauge,
                        MetricsNames.Gen1HeapSize, stats.Gen1Size);
                    var mp3 = new BinaryFileStorage.MetricPayload(BinaryFileStorage.MetricType.Gauge,
                        MetricsNames.Gen2HeapSize, stats.Gen2Size);
                    var mp4 = new BinaryFileStorage.MetricPayload(BinaryFileStorage.MetricType.Gauge,
                        MetricsNames.LohSize, stats.LohSize);
                    _storage.WritePayload(in mp1, in mp2, in mp3, in mp4);
                }
                else if (eventData.EventId == EventContentionStop)
                {
                    var durationInNanoseconds = (double)eventData.Payload[2];
                    IncrementTiming(ref _contentionTime, durationInNanoseconds / 1_000_000);
                    Interlocked.Increment(ref _contentionCount);
                }
                else if (eventData.EventId == EventGcGlobalHeapHistory)
                {
                    var heapHistory = HeapHistory.FromPayload(eventData.Payload);

                    if (heapHistory.MemoryLoad is { } memoryLoad)
                    {
                        var mp = new BinaryFileStorage.MetricPayload(BinaryFileStorage.MetricType.Gauge,
                            MetricsNames.GcMemoryLoad, memoryLoad);
                        _storage.WritePayload(in mp);
                    }

                    if (heapHistory.Generation == 0)
                    {
                        var mp = new BinaryFileStorage.MetricPayload(BinaryFileStorage.MetricType.Increment,
                            MetricsNames.Gen0CollectionsCount, 1);
                        _storage.WritePayload(in mp);
                    }
                    else if (heapHistory.Generation == 1)
                    {
                        var mp = new BinaryFileStorage.MetricPayload(BinaryFileStorage.MetricType.Increment,
                            MetricsNames.Gen1CollectionsCount, 1);
                        _storage.WritePayload(in mp);
                    }
                    else if (heapHistory.Generation == 2)
                    {
                        var mp = new BinaryFileStorage.MetricPayload(BinaryFileStorage.MetricType.Increment,
                            MetricsNames.Gen2CollectionsCount, 1);
                        _storage.WritePayload(in mp);
                        if (heapHistory.Compacting)
                        {
                            var mp2 = new BinaryFileStorage.MetricPayload(BinaryFileStorage.MetricType.Increment,
                                MetricsNames.Gen2CompactingCollectionsCount, 1);
                            _storage.WritePayload(in mp2);
                        }
                    }
                }
            }
        }
        catch
        {
            // .
        }
    }

    private void EnableEventSource(EventSource eventSource)
    {
        if (eventSource.Name == RuntimeEventSourceName)
        {
            EnableEvents(
                eventSource,
                EventLevel.Informational,
                (EventKeywords)(Keywords.GC | Keywords.Contention));
        }
        else if (eventSource.Name is AspNetCoreHostingEventSourceName or AspNetCoreKestrelEventSourceName)
        {
            EnableEvents(eventSource, EventLevel.Critical, EventKeywords.All, _eventCounterIntervalSecDictionary);
        }
    }

    private void ExtractCounters(ReadOnlyCollection<object> payload)
    {
        for (var i = 0; i < payload.Count; ++i)
        {
            if (payload[i] is not IDictionary<string, object> eventPayload)
            {
                continue;
            }

            if (!eventPayload.TryGetValue("Name", out var objName))
            {
                continue;
            }

            var name = objName as string ?? objName?.ToString() ?? string.Empty;
            if (!TryGetMetricsMapping(name, out var statName))
            {
                continue;
            }

            if (eventPayload.TryGetValue("Mean", out var rawValue) ||
                eventPayload.TryGetValue("Increment", out rawValue))
            {
                var mp = new BinaryFileStorage.MetricPayload(BinaryFileStorage.MetricType.Gauge, statName,
                    (double)rawValue);
                _storage.WritePayload(in mp);
            }
        }
    }

    private static bool TryGetMetricsMapping(string name, out ReadOnlySpan<byte> statName)
    {
        switch (name)
        {
            case "current-requests":
                statName = MetricsNames.AspNetCoreCurrentRequests;
                return true;
            case "failed-requests":
                statName = MetricsNames.AspNetCoreFailedRequests;
                return true;
            case "total-requests":
                statName = MetricsNames.AspNetCoreTotalRequests;
                return true;
            case "request-queue-length":
                statName = MetricsNames.AspNetCoreRequestQueueLength;
                return true;
            case "current-connections":
                statName = MetricsNames.AspNetCoreCurrentConnections;
                return true;
            case "connection-queue-length":
                statName = MetricsNames.AspNetCoreConnectionQueueLength;
                return true;
            case "total-connections":
                statName = MetricsNames.AspNetCoreTotalConnections;
                return true;
            default:
                statName = null;
                return false;
        }
    }

    private static void IncrementTiming(ref double cumulatedTiming, double elapsedMilliseconds)
    {
        double oldValue;

        do
        {
            oldValue = cumulatedTiming;
        }
        while (Math.Abs(Interlocked.CompareExchange(ref cumulatedTiming, oldValue + elapsedMilliseconds, oldValue) - oldValue) > 0.01);
    }
}
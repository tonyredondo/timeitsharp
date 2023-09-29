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

    private readonly FileStorage _storage;
    private readonly Timing _contentionTime = new();
    private readonly string _delayInSeconds;

    private long _contentionCount;
    private DateTime? _gcStart;

    public RuntimeEventListener(FileStorage storage, TimeSpan delay)
    {
        _storage = storage;
        _delayInSeconds = ((int)delay.TotalSeconds).ToString();
        EventSourceCreated += (_, e) => EnableEventSource(e.EventSource);
    }

    public void Refresh()
    {
        // Can't use a Timing because Dogstatsd doesn't support local aggregation
        // It means that the aggregations in the UI would be wrong
        _storage.Gauge(MetricsNames.ContentionTime, _contentionTime.Clear());
        _storage.Counter(MetricsNames.ContentionCount, Interlocked.Exchange(ref _contentionCount, 0));
        _storage.Gauge(MetricsNames.ThreadPoolWorkersCount, ThreadPool.ThreadCount);
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
                    _storage.Timer(MetricsNames.GcPauseTime, (eventData.TimeStamp - start).TotalMilliseconds);
                }
            }
            else
            {
                if (eventData.EventId == EventGcHeapStats)
                {
                    var stats = HeapStats.FromPayload(eventData.Payload);

                    _storage.Gauge(MetricsNames.Gen0HeapSize, stats.Gen0Size);
                    _storage.Gauge(MetricsNames.Gen1HeapSize, stats.Gen1Size);
                    _storage.Gauge(MetricsNames.Gen2HeapSize, stats.Gen2Size);
                    _storage.Gauge(MetricsNames.LohSize, stats.LohSize);
                }
                else if (eventData.EventId == EventContentionStop)
                {
                    var durationInNanoseconds = (double)eventData.Payload[2];

                    _contentionTime.Time(durationInNanoseconds / 1_000_000);
                    Interlocked.Increment(ref _contentionCount);
                }
                else if (eventData.EventId == EventGcGlobalHeapHistory)
                {
                    var heapHistory = HeapHistory.FromPayload(eventData.Payload);

                    if (heapHistory.MemoryLoad is { } memoryLoad)
                    {
                        _storage.Gauge(MetricsNames.GcMemoryLoad, memoryLoad);
                    }

                    if (heapHistory.Generation == 0)
                    {
                        _storage.Increment(MetricsNames.Gen0CollectionsCount, 1);
                    }
                    else if (heapHistory.Generation == 1)
                    {
                        _storage.Increment(MetricsNames.Gen1CollectionsCount, 1);
                    }
                    else if (heapHistory.Generation == 2)
                    {
                        _storage.Increment(MetricsNames.Gen2CollectionsCount, 1);
                        if (heapHistory.Compacting)
                        {
                            _storage.Increment(MetricsNames.Gen2CompactingCollectionsCount, 1);
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
            EnableEvents(eventSource, EventLevel.Critical, EventKeywords.All, new Dictionary<string, string>
            {
                ["EventCounterIntervalSec"] = _delayInSeconds
            });
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
                _storage.Gauge(statName, (double)rawValue);
            }
        }
    }

    private static bool TryGetMetricsMapping(string name, out string statName)
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
}
using System.Collections.Concurrent;
using DatadogTestLogger.Vendors.Datadog.Trace;
using DatadogTestLogger.Vendors.Datadog.Trace.Ci;
using DatadogTestLogger.Vendors.Datadog.Trace.Util;

namespace TimeItSharp.Common;

internal static class DatadogMetadata
{
    private static readonly ConcurrentDictionary<object, Metadata> MetadataByExecution = new();
    private static readonly object InitializationLock = new();
    private static bool? _useAllBits;

    internal static int InitializationCount => Volatile.Read(ref _initializationCount);
    private static int _initializationCount;

    public static void GetIds(object key, out TraceId traceId, out ulong spanId)
    {
        ArgumentNullException.ThrowIfNull(key);
        var useAllBits = GetUseAllBits();
        var value = MetadataByExecution.GetOrAdd(key, @case => new Metadata(RandomIdGenerator.Shared.NextTraceId(useAllBits),
            RandomIdGenerator.Shared.NextSpanId(useAllBits)));
        traceId = value.TraceId;
        spanId = value.SpanId;
    }

    private static bool GetUseAllBits()
    {
        if (_useAllBits is { } value)
        {
            return value;
        }

        lock (InitializationLock)
        {
            if (_useAllBits is not { } initializedValue)
            {
                CIVisibility.InitializeFromManualInstrumentation();
                Interlocked.Increment(ref _initializationCount);
                initializedValue = CIVisibility.Settings.TracerSettings?.TraceId128BitGenerationEnabled ?? true;
                _useAllBits = initializedValue;
            }

            return initializedValue;
        }
    }

    /// <summary>
    /// Releases the metadata associated with an execution key.
    /// </summary>
    /// <remarks>
    /// Execution keys are normally scenario instances. The metadata cache is process-wide so that
    /// the profiler and Datadog exporter can share span identifiers, but retaining every scenario
    /// forever would leak those instances in long-lived hosts. Release is deliberately idempotent;
    /// callers can invoke it from multiple cleanup paths without coordinating ownership. A null key
    /// is treated as already released because <see cref="ConcurrentDictionary{TKey, TValue}"/> does
    /// not accept null keys.
    /// </remarks>
    public static void Release(object? key)
    {
        if (key is not null)
        {
            MetadataByExecution.TryRemove(key, out _);
        }
    }

    private record struct Metadata(TraceId TraceId, ulong SpanId);
}

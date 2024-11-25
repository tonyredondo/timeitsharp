using System.Collections.Concurrent;
using DatadogTestLogger.Vendors.Datadog.Trace;
using DatadogTestLogger.Vendors.Datadog.Trace.Ci;
using DatadogTestLogger.Vendors.Datadog.Trace.Util;

namespace TimeItSharp.Common;

internal static class DatadogMetadata
{
    private static readonly ConcurrentDictionary<object, Metadata> MetadataByExecution;
    private static readonly bool UseAllBits;

    static DatadogMetadata()
    {
        MetadataByExecution = new();
        CIVisibility.InitializeFromManualInstrumentation();
        UseAllBits = CIVisibility.Settings.TracerSettings?.TraceId128BitGenerationEnabled ?? true;
    }

    public static void GetIds(object key, out TraceId traceId, out ulong spanId)
    {
        var value = MetadataByExecution.GetOrAdd(key, @case => new Metadata(RandomIdGenerator.Shared.NextTraceId(UseAllBits),
            RandomIdGenerator.Shared.NextSpanId(UseAllBits)));
        traceId = value.TraceId;
        spanId = value.SpanId;
    }

    private record struct Metadata(TraceId TraceId, ulong SpanId);
}
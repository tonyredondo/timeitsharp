using System.Collections.Concurrent;
using DatadogTestLogger.Vendors.Datadog.Trace;
using DatadogTestLogger.Vendors.Datadog.Trace.Ci;
using DatadogTestLogger.Vendors.Datadog.Trace.Util;

namespace TimeItSharp.Common;

internal static class DatadogMetadata
{
    private static readonly ConcurrentDictionary<object, Metadata> MetadataByExecution;

    static DatadogMetadata()
    {
        MetadataByExecution = new();
        CIVisibility.InitializeFromManualInstrumentation();
    }

    public static void GetIds(object key, out TraceId traceId, out ulong spanId)
    {
        var value = MetadataByExecution.GetOrAdd(key, @case => new());
        if (value.TraceId is null)
        {
            var useAllBits = CIVisibility.Settings.TracerSettings?.TraceId128BitGenerationEnabled ?? true;
            value.TraceId = RandomIdGenerator.Shared.NextTraceId(useAllBits);
            value.SpanId = RandomIdGenerator.Shared.NextSpanId(useAllBits);
        }

        traceId = value.TraceId.Value;
        spanId = value.SpanId;
    }

    private class Metadata
    {
        public TraceId? TraceId { get; set; }

        public ulong SpanId { get; set; }
    }
}
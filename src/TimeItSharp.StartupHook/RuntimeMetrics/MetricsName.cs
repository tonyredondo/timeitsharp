namespace TimeItSharp.RuntimeMetrics;

// The following code is based on: https://github.com/DataDog/dd-trace-dotnet/blob/master/tracer/src/Datadog.Trace/RuntimeMetrics

internal static class MetricsNames
{
    public static ReadOnlySpan<byte> ExceptionsCount => "runtime.dotnet.exceptions.count"u8;

    public static ReadOnlySpan<byte> Gen0CollectionsCount => "runtime.dotnet.gc.count.gen0"u8;
    public static ReadOnlySpan<byte> Gen1CollectionsCount => "runtime.dotnet.gc.count.gen1"u8;
    public static ReadOnlySpan<byte> Gen2CollectionsCount => "runtime.dotnet.gc.count.gen2"u8;
    public static ReadOnlySpan<byte> Gen2CompactingCollectionsCount => "runtime.dotnet.gc.count.compacting_gen2"u8;

    public static ReadOnlySpan<byte> GcPauseTime => "runtime.dotnet.gc.pause_time"u8;
    public static ReadOnlySpan<byte> GcMemoryLoad => "runtime.dotnet.gc.memory_load"u8;

    public static ReadOnlySpan<byte> Gen0HeapSize => "runtime.dotnet.gc.size.gen0"u8;
    public static ReadOnlySpan<byte> Gen1HeapSize => "runtime.dotnet.gc.size.gen1"u8;
    public static ReadOnlySpan<byte> Gen2HeapSize => "runtime.dotnet.gc.size.gen2"u8;
    public static ReadOnlySpan<byte> LohSize => "runtime.dotnet.gc.size.loh"u8;

    public static ReadOnlySpan<byte> ContentionTime => "runtime.dotnet.threads.contention_time"u8;
    public static ReadOnlySpan<byte> ContentionCount => "runtime.dotnet.threads.contention_count"u8;

    public static ReadOnlySpan<byte> ProcessorTime => "runtime.process.processor_time"u8;
    public static ReadOnlySpan<byte> PrivateBytes => "runtime.process.private_bytes"u8;
    
    public static ReadOnlySpan<byte> ThreadPoolWorkersCount => "runtime.dotnet.threads.workers_count"u8;

    public static ReadOnlySpan<byte> ThreadsCount => "runtime.dotnet.threads.count"u8;

    public static ReadOnlySpan<byte> CommittedMemory => "runtime.dotnet.mem.committed"u8;

    public static ReadOnlySpan<byte> CpuUserTime => "runtime.dotnet.cpu.user"u8;
    public static ReadOnlySpan<byte> CpuSystemTime => "runtime.dotnet.cpu.system"u8;
    public static ReadOnlySpan<byte> CpuPercentage => "runtime.dotnet.cpu.percent"u8;

    public static ReadOnlySpan<byte> AspNetCoreCurrentRequests => "runtime.dotnet.aspnetcore.requests.current"u8;
    public static ReadOnlySpan<byte> AspNetCoreFailedRequests => "runtime.dotnet.aspnetcore.requests.failed"u8;
    public static ReadOnlySpan<byte> AspNetCoreTotalRequests => "runtime.dotnet.aspnetcore.requests.total"u8;
    public static ReadOnlySpan<byte> AspNetCoreRequestQueueLength => "runtime.dotnet.aspnetcore.requests.queue_length"u8;

    public static ReadOnlySpan<byte> AspNetCoreCurrentConnections => "runtime.dotnet.aspnetcore.connections.current"u8;
    public static ReadOnlySpan<byte> AspNetCoreConnectionQueueLength => "runtime.dotnet.aspnetcore.connections.queue_length"u8;
    public static ReadOnlySpan<byte> AspNetCoreTotalConnections => "runtime.dotnet.aspnetcore.connections.total"u8;
}
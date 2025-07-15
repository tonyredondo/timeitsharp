using System.Runtime.CompilerServices;

namespace TimeItSharp.RuntimeMetrics;

internal sealed class BinaryFileStorage(string filePath)
{
    private readonly BinaryWriter _binaryWriter = new(new FileStream(filePath, FileMode.Append));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Counter(ReadOnlySpan<byte> name, double value)
    {
        WritePayload(MetricType.Counter, name, value);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Gauge(ReadOnlySpan<byte> name, double value)
    {
        WritePayload(MetricType.Gauge, name, value);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Increment(ReadOnlySpan<byte> name, double value)
    {
        WritePayload(MetricType.Increment, name, value);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Timer(ReadOnlySpan<byte> name, double value)
    {
        WritePayload(MetricType.Timer, name, value);
    }
    
    public void Dispose()
    {
        lock (_binaryWriter)
        {
            _binaryWriter.Dispose();
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WritePayload(MetricType type, ReadOnlySpan<byte> name, double value)
    {
        lock (_binaryWriter)
        {
            _binaryWriter.Write((int)7248); // Magic number for identification
            _binaryWriter.Write((byte)type); // Metric type
            _binaryWriter.Write(name.Length); // Length of the name
            _binaryWriter.Write(name); // Name of the metric
            _binaryWriter.Write(value); // Value of the metric
        }
    }
    
    internal enum MetricType : byte
    {
        Counter,
        Gauge,
        Increment,
        Timer
    }
}
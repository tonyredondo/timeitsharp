using System.Runtime.CompilerServices;

namespace TimeItSharp.RuntimeMetrics;

internal sealed class BinaryFileStorage(string filePath)
{
    private readonly BinaryWriter _binaryWriter = new(new FileStream(filePath, FileMode.Append));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WritePayload(in MetricPayload payload)
    {
        lock (_binaryWriter)
        {
            InternalWritePayload(in payload);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WritePayload(in MetricPayload payload1, in MetricPayload payload2)
    {
        lock (_binaryWriter)
        {
            InternalWritePayload(in payload1);
            InternalWritePayload(in payload2);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WritePayload(in MetricPayload payload1, in MetricPayload payload2, in MetricPayload payload3)
    {
        lock (_binaryWriter)
        {
            InternalWritePayload(in payload1);
            InternalWritePayload(in payload2);
            InternalWritePayload(in payload3);
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WritePayload(in MetricPayload payload1, in MetricPayload payload2, in MetricPayload payload3, in MetricPayload payload4)
    {
        lock (_binaryWriter)
        {
            InternalWritePayload(in payload1);
            InternalWritePayload(in payload2);
            InternalWritePayload(in payload3);
            InternalWritePayload(in payload4);
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WritePayload(in MetricPayload payload1, in MetricPayload payload2, in MetricPayload payload3, in MetricPayload payload4, in MetricPayload payload5, in MetricPayload payload6, in MetricPayload payload7, in MetricPayload payload8)
    {
        lock (_binaryWriter)
        {
            InternalWritePayload(in payload1);
            InternalWritePayload(in payload2);
            InternalWritePayload(in payload3);
            InternalWritePayload(in payload4);
            InternalWritePayload(in payload5);
            InternalWritePayload(in payload6);
            InternalWritePayload(in payload7);
            InternalWritePayload(in payload8);
        }
    }

    public void Dispose()
    {
        lock (_binaryWriter)
        {
            _binaryWriter.Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void InternalWritePayload(in MetricPayload payload)
    {
        _binaryWriter.Write((int)7248); // Magic number for identification
        _binaryWriter.Write((byte)payload.Type); // Metric type
        _binaryWriter.Write(payload.Name.Length); // Length of the name
        _binaryWriter.Write(payload.Name); // Name of the metric
        _binaryWriter.Write(payload.Value); // Value of the metric
    }

    internal enum MetricType : byte
    {
        Counter,
        Gauge,
        Increment,
        Timer
    }

    public readonly ref struct MetricPayload
    {
        public readonly MetricType Type;
        public readonly ReadOnlySpan<byte> Name;
        public readonly double Value;

        public MetricPayload(MetricType type, ReadOnlySpan<byte> name, double value)
        {
            Type = type;
            Name = name;
            Value = value;
        }
    }
}
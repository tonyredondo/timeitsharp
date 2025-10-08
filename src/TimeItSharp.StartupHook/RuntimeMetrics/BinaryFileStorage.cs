using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TimeItSharp.RuntimeMetrics;

internal sealed class BinaryFileStorage(string filePath)
{
    private readonly BinaryWriter _binaryWriter = new(new FileStream(filePath, FileMode.Append));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WritePayload(in MetricPayload payload)
    {
        lock (_binaryWriter)
        {
            payload.WriteTo(_binaryWriter);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WritePayload(in MetricPayload payload1, in MetricPayload payload2)
    {
        lock (_binaryWriter)
        {
            payload1.WriteTo(_binaryWriter);
            payload2.WriteTo(_binaryWriter);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WritePayload(in MetricPayload payload1, in MetricPayload payload2, in MetricPayload payload3)
    {
        lock (_binaryWriter)
        {
            payload1.WriteTo(_binaryWriter);
            payload2.WriteTo(_binaryWriter);
            payload3.WriteTo(_binaryWriter);
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WritePayload(in MetricPayload payload1, in MetricPayload payload2, in MetricPayload payload3, in MetricPayload payload4)
    {
        lock (_binaryWriter)
        {
            payload1.WriteTo(_binaryWriter);
            payload2.WriteTo(_binaryWriter);
            payload3.WriteTo(_binaryWriter);
            payload4.WriteTo(_binaryWriter);
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WritePayload(in MetricPayload payload1, in MetricPayload payload2, in MetricPayload payload3, in MetricPayload payload4, in MetricPayload payload5, in MetricPayload payload6, in MetricPayload payload7, in MetricPayload payload8)
    {
        lock (_binaryWriter)
        {
            payload1.WriteTo(_binaryWriter);
            payload2.WriteTo(_binaryWriter);
            payload3.WriteTo(_binaryWriter);
            payload4.WriteTo(_binaryWriter);
            payload5.WriteTo(_binaryWriter);
            payload6.WriteTo(_binaryWriter);
            payload7.WriteTo(_binaryWriter);
            payload8.WriteTo(_binaryWriter);
        }
    }

    public void Dispose()
    {
        lock (_binaryWriter)
        {
            _binaryWriter.Dispose();
        }
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
        private const int MagicNumber = 7248;
        private readonly MetricType _type;
        private readonly ReadOnlySpan<byte> _name;
        private readonly double _value;

        public MetricPayload(MetricType type, ReadOnlySpan<byte> name, double value)
        {
            _type = type;
            _name = name;
            _value = value;
        }
        
        public void WriteTo(BinaryWriter writer)
        {
            // Magic number (4 bytes) + Type (1 byte) + Name Length (4 bytes) + Name (variable) + Value (8 bytes)
            var payloadLength = 4 + 1 + 4 + _name.Length + 8; // Total length of the payload
            Span<byte> buffer = payloadLength <= 1024 ? stackalloc byte[payloadLength] : new byte[payloadLength];
            
            Unsafe.WriteUnaligned(ref buffer[0], MagicNumber); // Magic number for identification
            buffer[4] = (byte)_type; // Metric type
            Unsafe.WriteUnaligned(ref buffer[5], _name.Length); // Length of the name
            _name.CopyTo(buffer.Slice(9)); // Name of the metric
            Unsafe.WriteUnaligned(ref buffer[9 + _name.Length], _value); // Value of the metric
            writer.Write(buffer);
        }
    }
}
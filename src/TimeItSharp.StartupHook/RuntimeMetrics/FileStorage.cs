using System.Globalization;
using System.Runtime.CompilerServices;

namespace TimeItSharp.RuntimeMetrics;

internal sealed class FileStorage
{
    private readonly StreamWriter _streamWriter;

    public FileStorage(string filePath)
    {
        _streamWriter = new StreamWriter(filePath, true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Counter(string statName, double value)
    {
        WritePayload("counter", statName, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Gauge(string statName, double value)
    {
        WritePayload("gauge", statName, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Increment(string statName, int value = 1)
    {
        WritePayload("increment", statName, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Timer(string statName, double value)
    {
        WritePayload("timer", statName, value);
    }

    public void Dispose()
    {
        lock (_streamWriter)
        {
            _streamWriter.Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WritePayload(string type, string name, double value)
    {
        lock (_streamWriter)
        {
            _streamWriter.Write("{ \"type\": \"");
            _streamWriter.Write(type);
            _streamWriter.Write("\", ");

            _streamWriter.Write("\"name\": ");
            if (name is null)
            {
                _streamWriter.Write("null, ");
            }
            else
            {
                _streamWriter.Write("\"");
                _streamWriter.Write(name);
                _streamWriter.Write("\", ");
            }

            _streamWriter.Write("\"value\": ");
            _streamWriter.Write(value.ToString(CultureInfo.InvariantCulture));
            _streamWriter.WriteLine(" }");
        }
    }
}

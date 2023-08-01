﻿namespace TimeIt.RuntimeMetrics;

public class FileStatsd : IDogStatsd
{
    private readonly StreamWriter _streamWriter;

    public FileStatsd(string filePath)
    {
        _streamWriter = new StreamWriter(filePath, true);
    }

    public void Counter(string statName, double value, double sampleRate = 1, string[]? tags = null)
    {
        WritePayload("counter", statName, value);
    }

    public void Gauge(string statName, double value, double sampleRate = 1, string[]? tags = null)
    {
        WritePayload("gauge", statName, value);
    }

    public void Increment(string statName, int value = 1, double sampleRate = 1, string[]? tags = null)
    {
        WritePayload("increment", statName, value);
    }

    public void Timer(string statName, double value, double sampleRate = 1, string[]? tags = null)
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

    private void WritePayload(string type, string name, double value)
    {
        lock (_streamWriter)
        {
            _streamWriter.Write("{ \"type\": ");
            if (type is null)
            {
                _streamWriter.Write("null, ");
            }
            else
            {
                _streamWriter.Write("\"");
                _streamWriter.Write(type);
                _streamWriter.Write("\", ");
            }

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
            _streamWriter.Write(value);
            _streamWriter.WriteLine(" }");
        }
    }
}

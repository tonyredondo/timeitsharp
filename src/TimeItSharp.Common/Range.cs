using System.Text.Json.Serialization;

namespace TimeItSharp.Common;

public readonly struct Range<T>
{
    [JsonPropertyName("start")]
    public T Start { get; }

    [JsonPropertyName("end")]
    public T End { get; }

    public Range(T start, T end)
    {
        Start = start;
        End = end;
    }
}
using System.Diagnostics;
using System.Runtime.CompilerServices;

internal static class Clock
{
    private static readonly double DateTimeTickFrequency = 10000000.0 / Stopwatch.Frequency;
    private static readonly DateTime UtcStart;
    private static readonly long Timestamp;
    
    static Clock()
    {
        UtcStart = DateTime.UtcNow;
        Timestamp = Stopwatch.GetTimestamp();

        // The following is to prevent the case of GC hitting between UtcStart and Timestamp set
        var retries = 3;
        while ((DateTime.UtcNow - UtcNow).TotalMilliseconds > 16 && retries-- > 0)
        {
            UtcStart = DateTime.UtcNow;
            Timestamp = Stopwatch.GetTimestamp();
        }
    }

    public static DateTime UtcNow
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => UtcStart.AddTicks((long)((Stopwatch.GetTimestamp() - Timestamp) * DateTimeTickFrequency));
    }
}
namespace TimeIt.RuntimeMetrics;

// The following code is based on: https://github.com/DataDog/dd-trace-dotnet/blob/master/tracer/src/Datadog.Trace/RuntimeMetrics

public sealed class Timing
{
    private double _cumulatedMilliseconds;

    public void Time(double elapsedMilliseconds)
    {
        double oldValue;

        do
        {
            oldValue = _cumulatedMilliseconds;
        }
        while (Math.Abs(Interlocked.CompareExchange(ref _cumulatedMilliseconds, oldValue + elapsedMilliseconds, oldValue) - oldValue) > 0.01);
    }

    public double Clear()
    {
        return Interlocked.Exchange(ref _cumulatedMilliseconds, 0);
    }
}
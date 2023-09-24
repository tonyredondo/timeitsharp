using MathNet.Numerics.Statistics;

namespace TimeIt;

static class Utils
{
    public static IEnumerable<double> RemoveOutliers(IEnumerable<double> data, double threshold)
    {
        if (data is not List<double>)
        {
            data = data.ToList();
        }

        var stdDev = data.StandardDeviation();

        if (stdDev == 0.0 || double.IsNaN(stdDev))
        {
            return data;
        }

        var mean = data.Average();
        return data.Where(x => Math.Abs(x - mean) <= threshold * stdDev).ToList();
    }

    public static double FromNanosecondsToMilliseconds(double nanoseconds)
    {
        return TimeSpan.FromTicks((long)nanoseconds / 100).TotalMilliseconds;
    }

    public static double FromTimeSpanToNanoseconds(TimeSpan timeSpan)
    {
        return (double)timeSpan.Ticks * 100;
    }
}
namespace TimeItSharp.Common;

static class Utils
{
    public static double StandardDeviation(this IEnumerable<double> data)
    {
        var stdDev = MathNet.Numerics.Statistics.Statistics.StandardDeviation(data);
        if (double.IsNaN(stdDev))
        {
            return 0.0;
        }
        return stdDev;
    }

    public static IEnumerable<double> RemoveOutliers(IEnumerable<double> data, double threshold)
    {
        if (data is not List<double>)
        {
            data = data.ToList();
        }

        var stdDev = data.StandardDeviation();

        if (stdDev == 0.0)
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
using MathNet.Numerics.Statistics;

namespace TimeIt;

static class Utils
{
    public static string ReplaceCustomVars(string value)
    {
        return value.Replace("$(CWD)", Environment.CurrentDirectory);
    }

    public static IEnumerable<double> RemoveOutliers(IEnumerable<double> data, double threshold)
    {
        var mean = data.Average();
        var stdDev = data.StandardDeviation();
        return data.Where(x => Math.Abs(x - mean) <= threshold * stdDev).ToList();
    }
    
    public static double FromNanosecondsToMilliseconds(double nanoseconds)
    {
        return TimeSpan.FromTicks((long)nanoseconds / 100).TotalMilliseconds;
    }
}
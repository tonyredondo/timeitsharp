﻿using TimeItSharp.Common.Results;

namespace TimeItSharp.Common;

internal static class Utils
{
    /// <summary>
    /// Calculates the standard deviation of a sequence of double-precision floating-point numbers.
    /// </summary>
    /// <param name="data">The sequence of double-precision floating-point numbers.</param>
    /// <returns>The standard deviation, or 0.0 if the calculation results in NaN.</returns>
    public static double StandardDeviation(this IEnumerable<double> data)
    {
        // Use MathNet's StandardDeviation function to calculate the standard deviation
        var stdDev = MathNet.Numerics.Statistics.Statistics.StandardDeviation(data);
    
        // Check for NaN and return 0.0 if true
        if (double.IsNaN(stdDev))
        {
            return 0.0;
        }
    
        return stdDev;
    }

    /// <summary>
    /// Removes outliers from a sequence of double-precision floating-point numbers.
    /// </summary>
    /// <param name="data">The sequence of double-precision floating-point numbers.</param>
    /// <param name="threshold">The multiplier for the standard deviation. Data points outside of this range will be considered outliers.</param>
    /// <returns>A sequence with the outliers removed.</returns>
    public static IEnumerable<double> RemoveOutliers(IEnumerable<double> data, double threshold)
    {
        // Convert the sequence to a List if it's not already one
        var lstData = data as List<double> ?? data.ToList();

        // If the data is empty, return an empty array
        if (lstData.Count == 0)
        {
            return Array.Empty<double>();
        }

        // Calculate the standard deviation of the data
        var stdDev = lstData.StandardDeviation();

        // If the standard deviation is zero, return the original data
        if (stdDev == 0.0)
        {
            return lstData;
        }

        // Calculate the mean of the data
        var mean = lstData.Average();

        // Remove data points that are considered outliers based on the standard deviation and threshold
        return lstData.Where(x => Math.Abs(x - mean) <= threshold * stdDev).ToList();
    }

    /// <summary>
    /// Converts a time value given in nanoseconds to milliseconds.
    /// </summary>
    /// <param name="nanoseconds">The time in nanoseconds to be converted.</param>
    /// <returns>The time converted to milliseconds.</returns>
    public static double FromNanosecondsToMilliseconds(double nanoseconds)
    {
        // 1 tick is 100 nanoseconds
        var ticks = (long)(nanoseconds / 100.0);
        return TimeSpan.FromTicks(ticks).TotalMilliseconds;
    }

    /// <summary>
    /// Converts a TimeSpan object to nanoseconds.
    /// </summary>
    /// <param name="timeSpan">The TimeSpan object to be converted.</param>
    /// <returns>The time in nanoseconds.</returns>
    public static double FromTimeSpanToNanoseconds(TimeSpan timeSpan)
    {
        // 1 tick is 100 nanoseconds
        return (double)timeSpan.Ticks * 100;
    }

    /// <summary>
    /// Determines if a given dataset is bimodal.
    /// </summary>
    /// <param name="data">The dataset to analyze.</param>
    /// <param name="peakCount">Number of peaks detected</param>
    /// <param name="histogram">Histogram data</param>
    /// <param name="labels">Histogram labels</param>
    /// <param name="binCount">The number of bins to use for the histogram. Default is 10.</param>
    /// <returns>True if the dataset is bimodal, otherwise false.</returns>
    public static bool IsBimodal(Span<double> data, out int peakCount, out int[] histogram,  out Range<double>[] labels, int binCount = 10)
    {
        // Return false if there are less than 3 data points, as bimodality can't be determined.
        if (data.Length < 3 || binCount < 3)
        {
            peakCount = 0;
            histogram = Array.Empty<int>();
            labels = Array.Empty<Range<double>>();
            return false;
        }

        // Initialize variables to find the range of the data.
        double min = double.MaxValue, max = double.MinValue;

        // Find the minimum and maximum values in the data.
        foreach (var item in data)
        {
            if (item < min)
            {
                min = item;
            }

            if (item > max)
            {
                max = item;
            }
        }

        // Create and initialize a histogram with 'binCount' bins.
        histogram = new int[binCount];
        labels = new Range<double>[binCount];
        for (var i = 0; i < binCount; i++)
        {
            labels[i] = new(double.MaxValue, double.MinValue);
        }

        var binWidth = (max - min) / binCount;

        // Populate the histogram based on where each data point falls.
        foreach (var item in data)
        {
            var binIndex = (int)((item - min) / binWidth);
            // Handle edge case where item equals the maximum value.
            if (binIndex == binCount)
            {
                binIndex--;
            }

            histogram[binIndex]++;
            if (labels[binIndex].Start > item)
            {
                labels[binIndex] = new(item, labels[binIndex].End);
            }

            if (labels[binIndex].End < item)
            {
                labels[binIndex] = new(labels[binIndex].Start, item);
            }
        }

        // Initialize variable to count the number of peaks in the histogram.
        peakCount = 0;

        // Count the peaks in the histogram.
        // A peak is defined as a bin count greater than its neighbors.
        for (var i = 1; i < binCount - 1; i++)
        {
            if (histogram[i] - 1 > histogram[i - 1] && histogram[i] - 1 > histogram[i + 1])
            {
                peakCount++;
            }
        }

        // A dataset is considered bimodal if there are at least two peaks.
        return peakCount >= 2;
    }

    /// <summary>
    /// Calculates the interquartile range (IQR) of a sorted dataset.
    /// </summary>
    /// <param name="sortedData">The sorted dataset.</param>
    /// <returns>The IQR of the dataset.</returns>
    public static double CalculateIQR(double[] sortedData)
    {
        var n = sortedData.Length;
        double Q1, Q3;

        if (n % 2 == 0)
        {
            Q1 = (sortedData[n / 4] + sortedData[n / 4 - 1]) / 2.0;
            Q3 = (sortedData[3 * n / 4] + sortedData[3 * n / 4 - 1]) / 2.0;
        }
        else
        {
            Q1 = sortedData[n / 4];
            Q3 = sortedData[3 * n / 4];
        }

        return Q3 - Q1;
    }

    /// <summary>
    /// Generates a comparison table based on a list of ScenarioResult objects.
    /// Each cell [i, j] in the table contains the overhead percentage of the mean value of results[j] over results[i].
    /// </summary>
    /// <param name="results">A read-only list of ScenarioResult objects.</param>
    /// <returns>A 2D array containing the comparison data, or an empty array if the input list is null or empty.</returns>
    public static double[][] GetComparisonTableData(IReadOnlyList<ScenarioResult> results)
    {
        // Check if the results list is null or empty
        if (results is null || results.Count == 0)
        {
            return Array.Empty<double[]>();
        }

        // Initialize a 2D array to hold the comparison table data
        var tableData = new double[results.Count][];

        // Loop through each pair of results to populate the table
        for (var i = 0; i < results.Count; i++)
        {
            tableData[i] = new double[results.Count];
            for (var j = 0; j < results.Count; j++)
            {
                // Retrieve the mean values for the i-th and j-th results
                var firstItem = results[i];
                var firstMean = firstItem.Mean;

                var secondItem = results[j];
                var secondMean = secondItem.Mean;

                // Calculate the overhead of secondMean over firstMean and store it in the table
                var overhead = ((secondMean * 100) / firstMean) - 100;
                tableData[i][j] = Math.Round(overhead, 1);
            }
        }

        return tableData;
    }
    
    /// <summary>
    /// Retrieves the width of the console buffer safely. 
    /// If unable to determine the width, returns a default value.
    /// </summary>
    /// <param name="defaultValue">The default value to return if the width cannot be determined. Default is 180.</param>
    /// <returns>The width of the console buffer, or the default value if it cannot be determined.</returns>
    public static int GetSafeWidth(int defaultValue = 200)
    {
        try
        {
            // Attempt to get the console buffer width.
            var width = System.Console.BufferWidth;

            // If the buffer width is reported as zero, use the default value.
            if (width == 0)
            {
                width = defaultValue;
            }

            return width;
        }
        catch (IOException)
        {
            // If an IOException occurs (e.g., console not available), return the default value.
            return defaultValue;
        }
    }
}
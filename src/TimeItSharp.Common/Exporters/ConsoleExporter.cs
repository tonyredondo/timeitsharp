using System.Globalization;
using MathNet.Numerics.Statistics;
using Spectre.Console;
using TimeItSharp.Common.Results;
using Status = TimeItSharp.Common.Results.Status;

namespace TimeItSharp.Common.Exporters;

public sealed class ConsoleExporter : IExporter
{
    private InitOptions _options;

    public string Name => nameof(ConsoleExporter);

    public bool Enabled => true;

    public void Initialize(InitOptions options)
    {
        _options = options;
    }

    public void Export(TimeitResult results)
    {
        AnsiConsole.Profile.Width = Utils.GetSafeWidth();
        if (_options.Configuration is null)
        {
            AnsiConsole.MarkupLine("[red bold]Configuration is missing.[/]");
            return;
        }

        // ****************************************
        // Results table
        AnsiConsole.MarkupLine("[aqua bold underline]### Results:[/]");
        var resultsTable = new Table()
            .MarkdownBorder();
        
        // Add columns
        resultsTable.AddColumns(results.Scenarios.Select(r => new TableColumn($"[dodgerblue1 bold]{r.Name}[/]").Centered()).ToArray());
        
        // Add rows
        for (var i = 0; i < _options.Configuration.Count; i++)
        {
            resultsTable.AddRow(results.Scenarios.Select(r =>
            {
                if (i < r.Durations.Count)
                {
                    return Utils.FromNanosecondsToMilliseconds(r.Durations[i]) + "ms";
                }
                
                return "-";
            }).ToArray());
        }
        
        // Write table
        AnsiConsole.Write(resultsTable);
        
        // ****************************************
        // Outliers table
        var maxOutliersCount = results.Scenarios.Select(r => r.Outliers.Count).Max();
        if (maxOutliersCount > 0)
        {
            AnsiConsole.MarkupLine("[aqua bold underline]### Outliers:[/]");
            var outliersTable = new Table()
                .MarkdownBorder();

            // Add columns
            outliersTable.AddColumns(results.Scenarios
                .Select(r => new TableColumn($"[dodgerblue1 bold]{r.Name}[/]").Centered()).ToArray());

            // Add rows
            for (var i = 0; i < maxOutliersCount; i++)
            {
                outliersTable.AddRow(results.Scenarios.Select(r =>
                {
                    if (i < r.Outliers.Count)
                    {
                        return Utils.FromNanosecondsToMilliseconds(r.Outliers[i]) + "ms";
                    }

                    return "-";
                }).ToArray());
            }

            // Write table
            AnsiConsole.Write(outliersTable);
        }

        var resultsList = results.Scenarios.ToList();

        // Show distribution of results
        if (_options.Configuration.Count >= 10)
        {
            AnsiConsole.MarkupLine("[aqua bold underline]### Distribution:[/]");
            AnsiConsole.WriteLine();
            GenerateDistributionChart(results.Scenarios.ToDictionary(k => k.Name, v => v), 11);
        }

        // ****************************************
        // Summary table
        AnsiConsole.MarkupLine("[aqua bold underline]### Summary:[/]");
        var summaryTable = new Table()
            .MarkdownBorder();

        var additionalMetrics = results.Scenarios
            .SelectMany(s => s.AdditionalMetrics.Select(item => new { item.Key, item.Value, ScenarioResult = s }))
            .GroupBy(item => item.Key)
            .ToList();

        var columnList = new List<string>
        {
            "[dodgerblue1 bold]Name[/]",
            "[dodgerblue1 bold]Status[/]",
            "[dodgerblue1 bold]Mean[/]",
            "[dodgerblue1 bold]StdDev[/]",
            "[dodgerblue1 bold]StdErr[/]",
            "[dodgerblue1 bold]Min[/]",
            "[dodgerblue1 bold]Median[/]",
            "[dodgerblue1 bold]Max[/]",
            "[dodgerblue1 bold]P95[/]",
            "[dodgerblue1 bold]P90[/]",
            "[dodgerblue1 bold]Outliers[/]"
        };

        if (additionalMetrics.Count > 0)
        {
            foreach (var additionalMetric in additionalMetrics)
            {
                columnList.Add($"[dodgerblue1 bold]{additionalMetric.Key}[/]");
            }
        }
        
        // Add columns
        summaryTable.AddColumns(columnList.ToArray());

        // Add rows
        for (var idx = 0; idx < resultsList.Count; idx++)
        {
            var result = resultsList[idx];
            var totalNum = result.MetricsData.Count;
            if (totalNum > 0)
            {
                var outliersValue = result.Outliers.Count > 0 ? $"{result.Outliers.Count} {{{Math.Round(result.OutliersThreshold, 2)}}}" : "0";
                var rowList = new List<string>
                {
                    $"[aqua underline]{result.Name}[/]",
                    $"{(result.Status == Status.Passed ? "[aqua]Passed" : "[red]Failed")}[/]",
                    $"[aqua]{Math.Round(Utils.FromNanosecondsToMilliseconds(result.Mean), 6)}ms[/]",
                    $"[aqua]{Math.Round(Utils.FromNanosecondsToMilliseconds(result.Stdev), 6)}ms[/]",
                    $"[aqua]{Math.Round(Utils.FromNanosecondsToMilliseconds(result.StdErr), 6)}ms[/]",
                    $"[aqua]{Math.Round(Utils.FromNanosecondsToMilliseconds(result.Min), 6)}ms[/]",
                    $"[aqua]{Math.Round(Utils.FromNanosecondsToMilliseconds(result.Median), 6)}ms[/]",
                    $"[aqua]{Math.Round(Utils.FromNanosecondsToMilliseconds(result.Max), 6)}ms[/]",
                    $"[aqua]{Math.Round(Utils.FromNanosecondsToMilliseconds(result.P95), 6)}ms[/]",
                    $"[aqua]{Math.Round(Utils.FromNanosecondsToMilliseconds(result.P90), 6)}ms[/]",
                    $"[aqua]{outliersValue}[/]"
                };

                foreach (var additionalMetric in additionalMetrics)
                {
                    var metricValue = additionalMetric.FirstOrDefault(item => item.ScenarioResult == result);
                    rowList.Add(metricValue is null ? $"[aqua]-[/]" : $"[aqua]{Math.Round(metricValue.Value, 6)}[/]");
                }

                summaryTable.AddRow(rowList.ToArray());

                var orderedMetricsData = result.MetricsData.OrderBy(item => item.Key).ToList();
                for (var i = 0; i < totalNum; i++)
                {
                    var item = orderedMetricsData[i];
                    var itemResult = new List<double>();
                    var metricsOutliers = new List<double>();
                    var metricsThreshold = 0.5d;
                    while (metricsThreshold < 3.0d)
                    {
                        itemResult = Utils.RemoveOutliers(item.Value, metricsThreshold).ToList();
                        metricsOutliers = item.Value.Where(d => !itemResult.Contains(d)).ToList();
                        var outliersPercent = ((double)metricsOutliers.Count / item.Value.Count) * 100;
                        if (outliersPercent < 20)
                        {
                            // outliers must be not more than 20% of the data
                            break;
                        }

                        metricsThreshold += 0.1;
                    }
                    

                    var mMean = itemResult.Mean();
                    var mMedian = itemResult.Median();
                    var mStdDev = itemResult.StandardDeviation();
                    var mStdErr = mStdDev / Math.Sqrt(itemResult.Count);
                    var mMin = itemResult.Min();
                    var mMax = itemResult.Max();
                    var mP95 = itemResult.Percentile(95);
                    var mP90 = itemResult.Percentile(90);

                    string name;
                    if (i < totalNum - 1)
                    {
                        name = "  ├>" + item.Key;
                    }
                    else
                    {
                        name = "  └>" + item.Key;
                    }

                    summaryTable.AddRow(
                        name,
                        string.Empty,
                        Math.Round(mMean, 6).ToString(),
                        Math.Round(mStdDev, 6).ToString(),
                        Math.Round(mStdErr, 6).ToString(),
                        Math.Round(mMin, 6).ToString(),
                        Math.Round(mMedian, 6).ToString(),
                        Math.Round(mMax, 6).ToString(),
                        Math.Round(mP95, 6).ToString(),
                        Math.Round(mP90, 6).ToString(),
                        (metricsOutliers.Count == 0 ? "0" : metricsOutliers.Count + " {" + Math.Round(metricsThreshold, 2) + "}"));
                }
            }
            else
            {
                var outliersValue = result.Outliers.Count > 0 ? $"{result.Outliers.Count} {{{Math.Round(result.OutliersThreshold, 2)}}}" : "0";
                var rowList = new List<string>
                {
                    $"{result.Name}",
                    $"{(result.Status == Status.Passed ? "[aqua]Passed" : "[red]Failed")}[/]",
                    $"{Math.Round(Utils.FromNanosecondsToMilliseconds(result.Mean), 6)}ms",
                    $"{Math.Round(Utils.FromNanosecondsToMilliseconds(result.Stdev), 6)}ms",
                    $"{Math.Round(Utils.FromNanosecondsToMilliseconds(result.StdErr), 6)}ms",
                    $"{Math.Round(Utils.FromNanosecondsToMilliseconds(result.Min), 6)}ms",
                    $"{Math.Round(Utils.FromNanosecondsToMilliseconds(result.Median), 6)}ms",
                    $"{Math.Round(Utils.FromNanosecondsToMilliseconds(result.Max), 6)}ms",
                    $"{Math.Round(Utils.FromNanosecondsToMilliseconds(result.P95), 6)}ms",
                    $"{Math.Round(Utils.FromNanosecondsToMilliseconds(result.P90), 6)}ms",
                    $"{outliersValue}"
                };

                foreach (var additionalMetric in additionalMetrics)
                {
                    var metricValue = additionalMetric.FirstOrDefault(item => item.ScenarioResult == result);
                    rowList.Add(metricValue is null ? "-" : Math.Round(metricValue.Value, 6).ToString());
                }

                summaryTable.AddRow(rowList.ToArray());
            }
        }

        // Write table
        AnsiConsole.Write(summaryTable);
        AnsiConsole.WriteLine();
        
        
        // ******************************
        // Write overhead table

        if (results.Scenarios.Count > 1)
        {
            AnsiConsole.MarkupLine("[aqua bold underline]### Overheads:[/]");
            var overheadTable = new Table()
                .MarkdownBorder();

            var lstOverheadColumns = new List<string>();
            lstOverheadColumns.Add(string.Empty);
            foreach (var scenario in results.Scenarios)
            {
                lstOverheadColumns.Add($"[dodgerblue1 bold]{scenario.Name}[/]");
            }

            overheadTable.AddColumns(lstOverheadColumns.ToArray());
            for (var i = 0; i < results.Scenarios.Count; i++)
            {
                var row = new List<string>
                {
                    $"[dodgerblue1 bold]{results.Scenarios[i].Name}[/]"
                };

                for (var j = 0; j < results.Scenarios.Count; j++)
                {
                    var value = results.Overheads?[i][j] ?? 0;
                    if (value == 0)
                    {
                        row.Add("--");
                    }
                    else
                    {
                        row.Add($"{value.ToString(CultureInfo.InvariantCulture)}%");
                    }
                }

                overheadTable.AddRow(row.ToArray());
            }

            // Write overhead table
            AnsiConsole.Write(overheadTable);
            AnsiConsole.WriteLine();
        }

        // Write Errors
        for (var idx = 0; idx < resultsList.Count; idx++)
        {
            var result = resultsList[idx];
            if (!string.IsNullOrEmpty(result.Error))
            {
                if (result.Status == Status.Failed)
                {
                    AnsiConsole.MarkupLine("[red bold]Scenario '{0}':[/]{1}", result.Name, Environment.NewLine);
                    AnsiConsole.WriteLine(result.Error);
                }
                else
                {
                    AnsiConsole.MarkupLine("[green bold]Scenario '{0}':[/]{1}", result.Name, Environment.NewLine);
                    AnsiConsole.WriteLine(result.Error);
                }
            }
        }
    }
    
    static void GenerateDistributionChart(List<double> dataNanoseconds, int numBins)
    {
        // Check if the data array is empty
        if (dataNanoseconds == null || dataNanoseconds.Count == 0)
        {
            Console.WriteLine("No data available to generate the distribution chart.");
            return;
        }
        
        // Determine the unit based on the range
        var maxNanoSeconds = dataNanoseconds.Max();
        string unit;
        double scale;
        if (maxNanoSeconds >= 60_000_000_000)
        {
            unit = "m";
            scale = 60_000_000_000.0;
        }
        else if (maxNanoSeconds >= 1_000_000_000)
        {
            unit = "s";
            scale = 1_000_000_000.0;
        }
        else if (maxNanoSeconds >= 1_000_000)
        {
            unit = "ms";
            scale = 1_000_000.0;
        }
        else if (maxNanoSeconds >= 1_000)
        {
            unit = "μs";
            scale = 1_000.0;
        }
        else
        {
            unit = "ns";
            scale = 1.0;
        }

        // Scale data
        var data = dataNanoseconds.Select(ns => ns / scale).ToArray();
        
        // Find the minimum and maximum of the data
        var minData = data.Min();
        var maxData = data.Max();

        // Calculate the range and bin size
        var range = maxData - minData;

        // Avoid division by zero if all data points are equal
        if (range == 0)
        {
            range = 1;
        }

        var binSize = range / numBins;

        // Create bins and ranges
        var bins = new int[numBins];
        var binRanges = new List<Tuple<double, double>>();

        for (int i = 0; i < numBins; i++)
        {
            var start = Math.Round(minData + binSize * i, 4);
            var end = Math.Round(start + binSize, 4);
            binRanges.Add(Tuple.Create(start, end));
        }

        // Count data in each bin
        foreach (var dataPoint in data)
        {
            var binIndex = (int)Math.Floor((dataPoint - minData) / binSize);
            if (binIndex == numBins) binIndex = numBins - 1; // Include the maximum in the last bin
            bins[binIndex]++;
        }

        // Find the maximum count to normalize the chart
        var maxCount = bins.Max();

        // Generate the chart
        for (int i = 0; i < numBins; i++)
        {
            var start = binRanges[i].Item1;
            var end = binRanges[i].Item2;
            var count = bins[i];

            // Graphic representation
            var barLength = maxCount > 0 ? (int)Math.Round((double)count / maxCount * 30) : 0;
            var bar = new string('█', barLength);

            // Format the start and end values
            var startStr = (start.ToString("F4") + unit).PadLeft(12);
            var endStr = (end.ToString("F4") + unit).PadRight(12);
            Console.WriteLine($"{startStr} - {endStr}\u251c {bar} ({count})");
        }
    }
    
    static void GenerateDistributionChart(Dictionary<string, ScenarioResult> dataSeriesDict, int numBins)
    {
        // Check if the data series dictionary is null or empty
        if (dataSeriesDict == null || dataSeriesDict.Count == 0)
        {
            Console.WriteLine("No data available to generate the distribution chart.");
            return;
        }

        // Combine all durations from all series to find the overall minimum and maximum
        var allDataNanoseconds = dataSeriesDict.Values.SelectMany(series => series.Durations).ToList();

        // Determine the appropriate unit based on the maximum value
        var maxNanoSeconds = allDataNanoseconds.Max();
        string unit;
        double scale;
        if (maxNanoSeconds >= 60_000_000_000)
        {
            unit = "m";
            scale = 60_000_000_000.0;
        }
        else if (maxNanoSeconds >= 1_000_000_000)
        {
            unit = "s";
            scale = 1_000_000_000.0;
        }
        else if (maxNanoSeconds >= 1_000_000)
        {
            unit = "ms";
            scale = 1_000_000.0;
        }
        else if (maxNanoSeconds >= 1_000)
        {
            unit = "μs";
            scale = 1_000.0;
        }
        else
        {
            unit = "ns";
            scale = 1.0;
        }

        // Scale the data and store it in a new dictionary
        var scaledDataSeriesDict = new Dictionary<string, List<double>>();
        foreach (var kvp in dataSeriesDict)
        {
            var scaledData = kvp.Value.Durations.Select(ns => ns / scale).ToList();
            scaledDataSeriesDict[kvp.Key] = scaledData;
        }

        // Find the overall minimum and maximum of the scaled data
        var allScaledData = scaledDataSeriesDict.Values.SelectMany(series => series).ToList();
        var minData = allScaledData.Min();
        var maxData = allScaledData.Max();

        // Calculate the range and bin size
        var range = maxData - minData;

        // Avoid division by zero if all data points are equal
        if (range == 0)
        {
            range = 1;
        }

        var binSize = range / numBins;

        // Create bin ranges
        var binRanges = new List<Tuple<double, double>>();
        for (int i = 0; i < numBins; i++)
        {
            var start = Math.Round(minData + binSize * i, 4);
            var end = Math.Round(start + binSize, 4);
            binRanges.Add(Tuple.Create(start, end));
        }

        // Initialize bin counts for each series
        var binsPerSeries = new Dictionary<string, int[]>();
        foreach (var seriesLabel in scaledDataSeriesDict.Keys)
        {
            binsPerSeries[seriesLabel] = new int[numBins];
        }

        // Count data points in each bin for each series
        foreach (var kvp in scaledDataSeriesDict)
        {
            var seriesLabel = kvp.Key;
            var data = kvp.Value;
            var bins = binsPerSeries[seriesLabel];

            foreach (var dataPoint in data)
            {
                var binIndex = (int)Math.Floor((dataPoint - minData) / binSize);
                if (binIndex >= numBins) binIndex = numBins - 1; // Include the maximum in the last bin
                bins[binIndex]++;
            }
        }

        // Find the maximum bin count across all series for normalizing the bars
        var maxBinCount = binsPerSeries.Values.SelectMany(k => k).Max();

        // Assign unique characters to each series for differentiation
        var seriesChars = new Dictionary<string, char>();
        var availableChars = new[] { '█', '▒', '░', '■', '□', '▲', '●', '○', '◆', '◇', '★', '☆', '•', '◦', '▌', '▐', '▖', '▗', '▘', '▝', '▞', '▟' };
        var charIndex = 0;
        foreach (var seriesLabel in scaledDataSeriesDict.Keys)
        {
            seriesChars[seriesLabel] = availableChars[charIndex % availableChars.Length];
            charIndex++;
        }
        
        // Assign colors to each series
        var seriesColors = new Dictionary<string, string>();
        var availableColors = new[] { "red", "green", "blue", "yellow", "magenta", "cyan", "white" };
        int colorIndex = 0;
        foreach (var seriesLabel in scaledDataSeriesDict.Keys)
        {
            seriesColors[seriesLabel] = availableColors[colorIndex % availableColors.Length];
            colorIndex++;
        }

        // Generate the distribution chart
        var labelWidth = 27; // Adjust as necessary
        var barMaxLength = 40; // Maximum length of the bar

        for (var i = 0; i < numBins; i++)
        {
            var start = binRanges[i].Item1;
            var end = binRanges[i].Item2;

            // Format the bin range string
            var startStr = (start.ToString("F4") + unit).PadLeft(10);
            var endStr = (end.ToString("F4") + unit).PadRight(10);
            var rangeStr = $"{startStr} - {endStr}";
            rangeStr = rangeStr.PadLeft(labelWidth);

            var seriesCount = scaledDataSeriesDict.Keys.Count;
            var seriesIndex = 0;

            foreach (var seriesLabel in scaledDataSeriesDict.Keys)
            {
                var count = binsPerSeries[seriesLabel][i];
                var maxCount = maxBinCount;
                var barLength = maxCount > 0 ? (int)Math.Round((double)count / maxCount * barMaxLength) : 0;
                var barChar = seriesChars[seriesLabel];
                var barColor = seriesColors[seriesLabel];
                var bar = new string(barChar, barLength);

                var linePrefix = string.Empty.PadLeft(labelWidth + 1);

                if (seriesCount == 1)
                {
                    linePrefix = rangeStr + " ├ ";
                }
                else if (seriesIndex == 0)
                {
                    linePrefix += "┌ ";
                }
                else if (seriesIndex == seriesCount - 1)
                {
                    linePrefix += "└ ";
                }
                else
                {
                    linePrefix = rangeStr + " ├ ";
                }

                // Use AnsiConsole to print colored bars with counts
                AnsiConsole.MarkupLine(linePrefix + $"[{barColor}]{bar.PadRight(barMaxLength)} ({count})[/]");
                seriesIndex++;
            }
        }

        // Display the legend
        AnsiConsole.MarkupLine("  [aqua]Legend:[/]");
        foreach (var kvp in seriesChars)
        {
            if (dataSeriesDict.TryGetValue(kvp.Key, out var result))
            {
                if (result.IsBimodal)
                {
                    if (seriesColors.TryGetValue(kvp.Key, out var color))
                    {
                        AnsiConsole.MarkupLine($"    [{color}]{kvp.Value}[/] : [dodgerblue1 bold]{kvp.Key}[/]  [yellow bold]Bimodal with peak count: {result.PeakCount}[/]");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"    {kvp.Value} : [dodgerblue1 bold]{kvp.Key}[/]  [yellow bold]Bimodal with peak count: {result.PeakCount}[/]");
                    }
                }
                else
                {
                    if (seriesColors.TryGetValue(kvp.Key, out var color))
                    {
                        AnsiConsole.MarkupLine($"    [{color}]{kvp.Value}[/] : [dodgerblue1 bold]{kvp.Key}[/]");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"    {kvp.Value} : [dodgerblue1 bold]{kvp.Key}[/]");
                    }
                }
            }
        }
        AnsiConsole.MarkupLine($"  [aqua]Range: {range:F4}{unit}[/]");
        Console.WriteLine();
    }
}
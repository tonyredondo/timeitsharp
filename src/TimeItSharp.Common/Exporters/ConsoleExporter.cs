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
        AnsiConsole.MarkupLine("[aqua bold underline]### Results (last 10):[/]");
        var resultsTable = new Table()
            .MarkdownBorder();
        
        // Add columns
        resultsTable.AddColumns(results.Scenarios.Select(r => new TableColumn($"[dodgerblue1 bold]{r.Name}[/]").Centered()).ToArray());
        
        // Add rows
        var minDurationCount = Math.Min(results.Scenarios.Select(r => r.Durations.Count).Min(), 10);
        for (var i = minDurationCount; i > 0; i--)
        {
            resultsTable.AddRow(results.Scenarios.Select(r =>
            {
                if (i < r.Durations.Count)
                {
                    return Math.Round(Utils.FromNanosecondsToMilliseconds(r.Durations[^i]), 3) + "ms";
                }
                
                return "-";
            }).ToArray());
        }
        
        // Write table
        AnsiConsole.Write(resultsTable);
        
        // ****************************************
        // Outliers table
        var maxOutliersCount = Math.Min(results.Scenarios.Select(r => r.Outliers.Count).Max(), 5);
        if (maxOutliersCount > 0)
        {
            AnsiConsole.MarkupLine("[aqua bold underline]### Outliers (last 5):[/]");
            var outliersTable = new Table()
                .MarkdownBorder();

            // Add columns
            outliersTable.AddColumns(results.Scenarios
                .Select(r => new TableColumn($"[dodgerblue1 bold]{r.Name}[/]").Centered()).ToArray());

            // Add rows
            for (var i = maxOutliersCount; i > 0; i--)
            {
                outliersTable.AddRow(results.Scenarios.Select(r =>
                {
                    if (i < r.Outliers.Count)
                    {
                        return Math.Round(Utils.FromNanosecondsToMilliseconds(r.Outliers[^i]), 3) + "ms";
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
            "[dodgerblue1 bold]Median[/]",
            "[dodgerblue1 bold]C. Interval 100%[/]",
            "[dodgerblue1 bold]C. Interval 95%[/]",
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
                var outliersValue = result.Outliers.Count > 0 ? $"{result.Outliers.Count} {{{Math.Round(result.OutliersThreshold, 3)}}}" : "0";
                var rowList = new List<string>
                {
                    $"[aqua underline]{result.Name} [[N={result.Count}]][/]",
                    $"{(result.Status == Status.Passed ? "[aqua]Passed" : "[red]Failed")}[/]",
                    $"[aqua]{Math.Round(Utils.FromNanosecondsToMilliseconds(result.Mean), 3)}ms[/]",
                    $"[aqua]{Math.Round(Utils.FromNanosecondsToMilliseconds(result.Stdev), 3)}ms[/]",
                    $"[aqua]{Math.Round(Utils.FromNanosecondsToMilliseconds(result.StdErr), 3)}ms[/]",
                    $"[aqua]{Math.Round(Utils.FromNanosecondsToMilliseconds(result.Median), 3)}ms[/]",
                    Math.Abs(result.Min - result.Max) > 0.0001 ?
                        $"[aqua][[{Math.Round(Utils.FromNanosecondsToMilliseconds(result.Min), 3)} - {Math.Round(Utils.FromNanosecondsToMilliseconds(result.Max), 3)}]] ms[/]" :
                        $"[aqua]{Math.Round(Utils.FromNanosecondsToMilliseconds(result.Min), 3)}ms[/]",
                    Math.Abs(result.Ci95[0] - result.Ci95[1]) > 0.0001 ?
                        $"[aqua][[{Math.Round(Utils.FromNanosecondsToMilliseconds(result.Ci95[0]), 3)} - {Math.Round(Utils.FromNanosecondsToMilliseconds(result.Ci95[1]), 3)}]] ms[/]" :
                        $"[aqua]{Math.Round(Utils.FromNanosecondsToMilliseconds(result.Ci95[0]), 3)}ms[/]",
                    $"[aqua]{outliersValue}[/]"
                };

                foreach (var additionalMetric in additionalMetrics)
                {
                    var metricValue = additionalMetric.FirstOrDefault(item => item.ScenarioResult == result);
                    rowList.Add(metricValue is null ? $"[aqua]-[/]" : $"[aqua]{Math.Round(metricValue.Value, 3)}[/]");
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
                    var ci95 = Utils.CalculateConfidenceInterval(mMean, mStdErr, itemResult.Count, 0.95);

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
                        Math.Round(mMean, 3).ToString(CultureInfo.InvariantCulture),
                        Math.Round(mStdDev, 3).ToString(CultureInfo.InvariantCulture),
                        Math.Round(mStdErr, 3).ToString(CultureInfo.InvariantCulture),
                        Math.Round(mMedian, 3).ToString(CultureInfo.InvariantCulture),
                        Math.Abs(mMin - mMax) > 0.0001 ?
                            $"[[{Math.Round(mMin, 3).ToString(CultureInfo.InvariantCulture)} - {Math.Round(mMax, 3).ToString(CultureInfo.InvariantCulture)}]]" : 
                            Math.Round(mMin, 3).ToString(CultureInfo.InvariantCulture),
                        Math.Abs(ci95[0] - ci95[1]) > 0.0001 ?
                            $"[[{Math.Round(ci95[0], 3).ToString(CultureInfo.InvariantCulture)} - {Math.Round(ci95[1], 3).ToString(CultureInfo.InvariantCulture)}]]" : 
                            Math.Round(ci95[0], 3).ToString(CultureInfo.InvariantCulture),
                        (metricsOutliers.Count == 0 ? "0" : metricsOutliers.Count + " {" + Math.Round(metricsThreshold, 3) + "}"));
                }
                
                if (resultsList.Count - idx > 1)
                {
                    summaryTable.AddEmptyRow();
                }
            }
            else
            {
                var outliersValue = result.Outliers.Count > 0 ? $"{result.Outliers.Count} {{{Math.Round(result.OutliersThreshold, 3)}}}" : "0";
                var rowList = new List<string>
                {
                    $"{result.Name} [[N={result.Count}]]",
                    $"{(result.Status == Status.Passed ? "[aqua]Passed" : "[red]Failed")}[/]",
                    $"{Math.Round(Utils.FromNanosecondsToMilliseconds(result.Mean), 3)}ms",
                    $"{Math.Round(Utils.FromNanosecondsToMilliseconds(result.Stdev), 3)}ms",
                    $"{Math.Round(Utils.FromNanosecondsToMilliseconds(result.StdErr), 3)}ms",
                    $"{Math.Round(Utils.FromNanosecondsToMilliseconds(result.Median), 3)}ms",
                    Math.Abs(result.Min - result.Max) > 0.0001 ?
                        $"[[{Math.Round(Utils.FromNanosecondsToMilliseconds(result.Min), 3)} - {Math.Round(Utils.FromNanosecondsToMilliseconds(result.Max), 3)}]] ms" :
                        $"{Math.Round(Utils.FromNanosecondsToMilliseconds(result.Min), 3)}ms",
                    Math.Abs(result.Ci95[0] - result.Ci95[1]) > 0.0001 ?
                        $"[[{Math.Round(Utils.FromNanosecondsToMilliseconds(result.Ci95[0]), 3)} - {Math.Round(Utils.FromNanosecondsToMilliseconds(result.Ci95[1]), 3)}]] ms" :
                        $"{Math.Round(Utils.FromNanosecondsToMilliseconds(result.Ci95[0]), 3)}ms",
                    $"{outliersValue}"
                };

                foreach (var additionalMetric in additionalMetrics)
                {
                    var metricValue = additionalMetric.FirstOrDefault(item => item.ScenarioResult == result);
                    rowList.Add(metricValue is null ? "-" : Math.Round(metricValue.Value, 3).ToString(CultureInfo.InvariantCulture));
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

            var lstOverheadColumns = new List<string> { string.Empty };
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
                    var value = results.Overheads?[i][j] ?? default;
                    if (value.OverheadPercentage == 0)
                    {
                        row.Add("--");
                    }
                    else
                    {
                        row.Add($"{value.OverheadPercentage.ToString(CultureInfo.InvariantCulture)}% ({Math.Round(Utils.FromNanosecondsToMilliseconds(value.DeltaValue), 3)}ms)");
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

        // Determine the number of decimal places based on binSize
        int decimalPlaces = binSize >= 1 ? 1 : (int)Math.Ceiling(-Math.Log10(binSize)) + 1;

        // Create bin edges without rounding
        var binEdges = new List<double>();
        for (int i = 0; i <= numBins; i++) // Need numBins + 1 edges
        {
            binEdges.Add(minData + binSize * i);
        }

        // Initialize bin counts for each series
        var binsPerSeries = new Dictionary<string, int[]>();
        foreach (var seriesLabel in scaledDataSeriesDict.Keys)
        {
            binsPerSeries[seriesLabel] = new int[numBins];
        }

        // Count data points in each bin for each series using precise edges
        foreach (var kvp in scaledDataSeriesDict)
        {
            var seriesLabel = kvp.Key;
            var data = kvp.Value;
            var bins = binsPerSeries[seriesLabel];

            foreach (var dataPoint in data)
            {
                var binIndex = (int)((dataPoint - minData) / binSize);
                if (binIndex >= numBins) binIndex = numBins - 1; // Include the maximum in the last bin
                bins[binIndex]++;
            }
        } 

        // Check if distributions are overlapping or not
        // Simplified overlapping detection
        var overlappingBinsThreshold = 4; // Set your desired threshold here
        var overlappingBinsCount = 0;

        for (var i = 0; i < numBins; i++)
        {
            var seriesWithCounts = 0;
            foreach (var bins in binsPerSeries.Values)
            {
                if (bins[i] > 0)
                    seriesWithCounts++;
            }
            if (seriesWithCounts > 1)
                overlappingBinsCount++;
        }

        var plotSeparately = overlappingBinsCount < overlappingBinsThreshold;

        // Assign unique characters to each series for differentiation
        var seriesChars = new Dictionary<string, char>();
        var availableChars = new[]
        {
            '█', '▒', '░', '■', '□', '▲', '●', '○', '◆', '◇', '★', '☆', '•', '◦', '▌', '▐', '▖', '▗', '▘', '▝', '▞', '▟'
        };
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

        if (plotSeparately)
        {
            // Plot histograms separately for each series
            foreach (var seriesLabel in scaledDataSeriesDict.Keys)
            {
                var data = scaledDataSeriesDict[seriesLabel];
                // Compute minData and maxData for this series
                var seriesMinData = data.Min();
                var seriesMaxData = data.Max();

                // Determine unit and scale for this series based on its data
                string seriesUnit;
                double seriesScale;
                if (seriesMaxData >= 60_000_000_000.0 / scale)
                {
                    seriesUnit = "m";
                    seriesScale = 60_000_000_000.0 / scale;
                }
                else if (seriesMaxData >= 1_000_000_000.0 / scale)
                {
                    seriesUnit = "s";
                    seriesScale = 1_000_000_000.0 / scale;
                }
                else if (seriesMaxData >= 1_000_000.0 / scale)
                {
                    seriesUnit = "ms";
                    seriesScale = 1_000_000.0 / scale;
                }
                else if (seriesMaxData >= 1_000.0 / scale)
                {
                    seriesUnit = "μs";
                    seriesScale = 1_000.0 / scale;
                }
                else
                {
                    seriesUnit = "ns";
                    seriesScale = 1.0 / scale;
                }

                // Re-scale data if necessary
                if (seriesScale != 1.0)
                {
                    data = data.Select(d => d / seriesScale).ToList();
                    seriesMinData = data.Min();
                    seriesMaxData = data.Max();
                }

                // Calculate the range and bin size
                var seriesRange = seriesMaxData - seriesMinData;

                // Avoid division by zero if all data points are equal
                if (seriesRange == 0)
                {
                    seriesRange = 1;
                }

                var seriesBinSize = seriesRange / numBins;

                // Determine the number of decimal places based on binSize
                int seriesDecimalPlaces = seriesBinSize >= 1 ? 1 : (int)Math.Ceiling(-Math.Log10(seriesBinSize)) + 1;

                // Create bin edges without rounding
                var seriesBinEdges = new List<double>();
                for (int i = 0; i <= numBins; i++) // Need numBins + 1 edges
                {
                    seriesBinEdges.Add(seriesMinData + seriesBinSize * i);
                }

                // Initialize bin counts
                var seriesBins = new int[numBins];

                // Count data points in bins
                foreach (var dataPoint in data)
                {
                    var binIndex = (int)((dataPoint - seriesMinData) / seriesBinSize);
                    if (binIndex >= numBins) binIndex = numBins - 1; // Include the maximum in the last bin
                    seriesBins[binIndex]++;
                }

                // Generate bin ranges for display, applying rounding only here
                var binRanges = new List<Tuple<double, double>>();
                for (int i = 0; i < numBins; i++)
                {
                    var start = Math.Round(seriesBinEdges[i], seriesDecimalPlaces);
                    var end = Math.Round(seriesBinEdges[i + 1], seriesDecimalPlaces);
                    binRanges.Add(Tuple.Create(start, end));
                }

                // Find the maximum bin count for normalizing the bars
                var maxBinCount = seriesBins.Max();

                // Generate the distribution chart for this series
                var labelWidth = 27; // Adjust as necessary
                var barMaxLength = 40; // Maximum length of the bar

                var formatStr = "F" + seriesDecimalPlaces; // Format string for decimal places

                for (var i = 0; i < numBins; i++)
                {
                    var start = binRanges[i].Item1;
                    var end = binRanges[i].Item2;

                    // Format the bin range string
                    var startStr = (start.ToString(formatStr) + seriesUnit).PadLeft(10);
                    var endStr = (end.ToString(formatStr) + seriesUnit).PadRight(10);
                    var rangeStr = $"{startStr} - {endStr}";
                    rangeStr = rangeStr.PadLeft(labelWidth);

                    var count = seriesBins[i];
                    var barLength = maxBinCount > 0 ? (int)Math.Round((double)count / maxBinCount * barMaxLength) : 0;
                    var barChar = seriesChars[seriesLabel];
                    var barColor = seriesColors[seriesLabel];
                    var bar = new string(barChar, barLength);

                    // Use AnsiConsole to print colored bars with counts
                    AnsiConsole.MarkupLine(rangeStr + " ├ " + $"[{barColor}]{bar.PadRight(barMaxLength)} ({count})[/]");
                }

                // Display the legend
                AnsiConsole.MarkupLine("  [aqua]Legend:[/]");
                if (dataSeriesDict.TryGetValue(seriesLabel, out var result))
                {
                    if (result.IsBimodal)
                    {
                        if (seriesColors.TryGetValue(seriesLabel, out var color))
                        {
                            AnsiConsole.MarkupLine(
                                $"    [{color}]{seriesChars[seriesLabel]}[/] : [dodgerblue1 bold]{seriesLabel}[/]  [yellow bold]Bimodal with peak count: {result.PeakCount}[/]");
                        }
                        else
                        {
                            AnsiConsole.MarkupLine(
                                $"    {seriesChars[seriesLabel]} : [dodgerblue1 bold]{seriesLabel}[/]  [yellow bold]Bimodal with peak count: {result.PeakCount}[/]");
                        }
                    }
                    else
                    {
                        if (seriesColors.TryGetValue(seriesLabel, out var color))
                        {
                            AnsiConsole.MarkupLine(
                                $"    [{color}]{seriesChars[seriesLabel]}[/] : [dodgerblue1 bold]{seriesLabel}[/]");
                        }
                        else
                        {
                            AnsiConsole.MarkupLine(
                                $"    {seriesChars[seriesLabel]} : [dodgerblue1 bold]{seriesLabel}[/]");
                        }
                    }
                }

                // Display the range
                AnsiConsole.MarkupLine($"  [aqua]Range: {seriesRange.ToString(formatStr)}{seriesUnit}[/]");
                AnsiConsole.WriteLine();
            }
        }
        else
        {
            // Generate bin ranges for display, applying rounding only here
            var binRanges = new List<Tuple<double, double>>();
            for (int i = 0; i < numBins; i++)
            {
                var start = Math.Round(binEdges[i], decimalPlaces);
                var end = Math.Round(binEdges[i + 1], decimalPlaces);
                binRanges.Add(Tuple.Create(start, end));
            }

            // Find the maximum bin count across all series for normalizing the bars
            var maxBinCount = binsPerSeries.Values.SelectMany(k => k).Max();

            // Generate the distribution chart
            var labelWidth = 27; // Adjust as necessary
            var barMaxLength = 40; // Maximum length of the bar

            var formatStr = "F" + decimalPlaces; // Format string for decimal places

            for (var i = 0; i < numBins; i++)
            {
                var start = binRanges[i].Item1;
                var end = binRanges[i].Item2;

                // Format the bin range string
                var startStr = (start.ToString(formatStr) + unit).PadLeft(10);
                var endStr = (end.ToString(formatStr) + unit).PadRight(10);
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
                        if (seriesCount == 2)
                        {
                            linePrefix = rangeStr + " ┌ ";
                        }
                        else
                        {
                            linePrefix += "┌ ";
                        }
                    }
                    else if (seriesIndex == seriesCount - 1)
                    {
                        linePrefix += "└ ";
                    }
                    else if (seriesIndex == seriesCount / 2)
                    {
                        linePrefix = rangeStr + " ┤ ";
                    }
                    else
                    {
                        linePrefix += "│ ";
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
                            AnsiConsole.MarkupLine(
                                $"    [{color}]{kvp.Value}[/] : [dodgerblue1 bold]{kvp.Key}[/]  [yellow bold]Bimodal with peak count: {result.PeakCount}[/]");
                        }
                        else
                        {
                            AnsiConsole.MarkupLine(
                                $"    {kvp.Value} : [dodgerblue1 bold]{kvp.Key}[/]  [yellow bold]Bimodal with peak count: {result.PeakCount}[/]");
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

            // Display the overall range
            AnsiConsole.MarkupLine($"  [aqua]Range: {range.ToString(formatStr)}{unit}[/]");
            AnsiConsole.WriteLine();
        }
    }
}
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
            for (var idx = 0; idx < resultsList.Count; idx++)
            {
                var result = resultsList[idx];
                AnsiConsole.MarkupLine($"[dodgerblue1 bold]{result.Scenario?.Name}:[/]");
                if (result.IsBimodal)
                {
                    AnsiConsole.MarkupLine("[yellow bold]Scenario '{0}' is Bimodal.[/] Peak count: {1}", result.Name,
                        result.PeakCount);
                }

                GenerateDistributionChart(result.Durations, 10);
                AnsiConsole.WriteLine();
            }
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
        
        // Convert data from nanoseconds to milliseconds
        double[] data = dataNanoseconds.Select(ns => ns / 1_000_000.0).ToArray();

        // Find the minimum and maximum of the data
        double minData = data.Min();
        double maxData = data.Max();

        // Calculate the range and bin size
        double range = maxData - minData;

        // Avoid division by zero if all data points are equal
        if (range == 0)
        {
            range = 1;
        }

        double binSize = range / numBins;

        // Create bins and ranges
        var bins = new int[numBins];
        var binRanges = new List<Tuple<double, double>>();

        for (int i = 0; i < numBins; i++)
        {
            double start = minData + binSize * i;
            double end = start + binSize;
            binRanges.Add(Tuple.Create(start, end));
        }

        // Count data in each bin
        foreach (var dataPoint in data)
        {
            int binIndex = (int)((dataPoint - minData) / binSize);
            if (binIndex == numBins) binIndex = numBins - 1; // Include the maximum in the last bin
            bins[binIndex]++;
        }

        // Find the maximum count to normalize the chart
        int maxCount = bins.Max();

        // Generate the chart
        for (int i = 0; i < numBins; i++)
        {
            double start = binRanges[i].Item1;
            double end = binRanges[i].Item2;
            int count = bins[i];

            // Graphic representation
            int barLength = maxCount > 0 ? (int)Math.Round((double)count / maxCount * 30) : 0;
            string bar = new string('█', barLength);

            // Format the start and end values
            
            string startStr = (start.ToString("F4") + "ms").PadLeft(12);
            string endStr = (end.ToString("F4") + "ms").PadRight(12);

            Console.WriteLine($"{startStr} - {endStr}| {bar} ({count})");
        }
    }
}
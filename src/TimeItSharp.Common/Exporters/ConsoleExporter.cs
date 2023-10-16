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
        AnsiConsole.MarkupLine("[aqua bold underline]### Outliers:[/]");
        var outliersTable = new Table()
            .MarkdownBorder();
        
        // Add columns
        outliersTable.AddColumns(results.Scenarios.Select(r => new TableColumn($"[dodgerblue1 bold]{r.Name}[/]").Centered()).ToArray());

        // Add rows
        var maxOutliersCount = results.Scenarios.Select(r => r.Outliers.Count).Max();
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
        var resultsList = results.Scenarios.ToList();
        for (var idx = 0; idx < resultsList.Count; idx++)
        {
            var result = resultsList[idx];
            var totalNum = result.MetricsData.Count;
            if (totalNum > 0)
            {
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
                    $"[aqua]{result.Outliers.Count}[/]"
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
                    var itemResult = Utils.RemoveOutliers(item.Value, 3).ToList();
                    int? outliersCount = item.Value.Count - itemResult.Count;
                    if (outliersCount > (_options.Configuration.Count * 5) / 100)
                    {
                        itemResult = item.Value;
                        outliersCount = null;
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
                        outliersCount?.ToString() ?? "N/A");
                }
            }
            else
            {
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
                    $"{result.Outliers.Count}"
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

        // Check if is bimodal
        for (var idx = 0; idx < resultsList.Count; idx++)
        {
            var result = resultsList[idx];
            if (result.IsBimodal)
            {
                AnsiConsole.MarkupLine("[yellow bold]Scenario '{0}' is Bimodal.[/] Peak count: {1}", result.Name, result.PeakCount);
                WriteHistogramHorizontal(result.Histogram, result.HistogramLabels);
                AnsiConsole.WriteLine();
            }
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

    private static void WriteHistogramHorizontal(int[] data, Range<double>[] labels)
    {
        int maxVal = Int32.MinValue;
        for (int i = 0; i < data.Length; i++)
        {
            if (data[i] > maxVal)
            {
                maxVal = data[i];
            }
        }

        for (int i = 0; i < data.Length; i++)
        {
            string bar = new string('█', data[i] * 10 / maxVal);  // Escalar el valor
            AnsiConsole.MarkupLine($" [green]{Math.Round(Utils.FromNanosecondsToMilliseconds(labels[i].Start), 4):0.0000}ms - {Math.Round(Utils.FromNanosecondsToMilliseconds(labels[i].End), 4):0.0000}ms[/]: [blue]{bar}[/] ({data[i]})");
        }
    }
}
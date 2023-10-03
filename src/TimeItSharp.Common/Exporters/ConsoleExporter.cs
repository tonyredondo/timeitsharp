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

    public void Export(IEnumerable<ScenarioResult> results)
    {
        if (_options.Configuration is null)
        {
            AnsiConsole.MarkupLine("[red bold]Configuration is missing.[/]");
            return;
        }

        // We make sure we are enumerating at least 1 time.
        if (results is not List<ScenarioResult>)
        {
            results = results.ToList();
        }

        // ****************************************
        // Results table
        AnsiConsole.MarkupLine("[aqua bold underline]### Results:[/]");
        var resultsTable = new Table()
            .MarkdownBorder();
        
        // Add columns
        resultsTable.AddColumns(results.Select(r => new TableColumn($"[dodgerblue1 bold]{r.Name}[/]").Centered()).ToArray());
        
        // Add rows
        for (var i = 0; i < _options.Configuration.Count; i++)
        {
            resultsTable.AddRow(results.Select(r =>
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
        outliersTable.AddColumns(results.Select(r => new TableColumn($"[dodgerblue1 bold]{r.Name}[/]").Centered()).ToArray());

        // Add rows
        var maxOutliersCount = results.Select(r => r.Outliers.Count).Max();
        for (var i = 0; i < maxOutliersCount; i++)
        {
            outliersTable.AddRow(results.Select(r =>
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
        
        // Add columns
        summaryTable.AddColumns(
            "[dodgerblue1 bold]Name[/]",
            "[dodgerblue1 bold]Status[/]",
            "[dodgerblue1 bold]Mean[/]",
            "[dodgerblue1 bold]StdDev[/]",
            "[dodgerblue1 bold]StdErr[/]",
            "[dodgerblue1 bold]Min[/]",
            "[dodgerblue1 bold]Max[/]",
            "[dodgerblue1 bold]P95[/]",
            "[dodgerblue1 bold]P90[/]",
            "[dodgerblue1 bold]Outliers[/]");

        // Add rows
        var resultsList = results.ToList();
        for (var idx = 0; idx < resultsList.Count; idx++)
        {
            var result = resultsList[idx];
            var totalNum = result.MetricsData.Count;

            if (totalNum > 0)
            {
                summaryTable.AddRow(
                    $"[aqua underline]{result.Name}[/]",
                    $"{(result.Status == Status.Passed ? "[aqua]Passed" : "[red]Failed")}[/]",
                    $"[aqua]{Utils.FromNanosecondsToMilliseconds(result.Mean)}ms[/]",
                    $"[aqua]{Utils.FromNanosecondsToMilliseconds(result.Stdev)}ms[/]",
                    $"[aqua]{Utils.FromNanosecondsToMilliseconds(result.StdErr)}ms[/]",
                    $"[aqua]{Utils.FromNanosecondsToMilliseconds(result.Min)}ms[/]",
                    $"[aqua]{Utils.FromNanosecondsToMilliseconds(result.Max)}ms[/]",
                    $"[aqua]{Utils.FromNanosecondsToMilliseconds(result.P95)}ms[/]",
                    $"[aqua]{Utils.FromNanosecondsToMilliseconds(result.P90)}ms[/]",
                    $"[aqua]{result.Outliers.Count}[/]");

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
                        Math.Round(mMax, 6).ToString(),
                        Math.Round(mP95, 6).ToString(),
                        Math.Round(mP90, 6).ToString(),
                        outliersCount?.ToString() ?? "N/A");
                }
            }
            else
            {
                summaryTable.AddRow(
                    result.Name,
                    $"{Utils.FromNanosecondsToMilliseconds(result.Mean)}ms",
                    $"{Utils.FromNanosecondsToMilliseconds(result.Stdev)}ms",
                    $"{Utils.FromNanosecondsToMilliseconds(result.StdErr)}ms",
                    $"{Utils.FromNanosecondsToMilliseconds(result.P99)}ms",
                    $"{Utils.FromNanosecondsToMilliseconds(result.P95)}ms",
                    $"{Utils.FromNanosecondsToMilliseconds(result.P90)}ms",
                    result.Outliers.Count.ToString());
            }
        }

        // Write table
        AnsiConsole.Write(summaryTable);
        AnsiConsole.WriteLine();

        // Write Errors
        for (var idx = 0; idx < resultsList.Count; idx++)
        {
            var result = resultsList[idx];
            if (!string.IsNullOrEmpty(result.Error))
            {
                if (result.Status == Status.Failed)
                {
                    AnsiConsole.MarkupLine("[red bold]Scenario '{0}':[/]{1}{2}", result.Name, Environment.NewLine, result.Error);
                }
                else
                {
                    AnsiConsole.MarkupLine("[green bold]Scenario '{0}':[/]{1}{2}", result.Name, Environment.NewLine, result.Error);
                }
            }
        }
    }
}
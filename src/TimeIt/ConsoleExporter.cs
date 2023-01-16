using MathNet.Numerics.Statistics;
using Spectre.Console;
using TimeIt.Common.Configuration;
using TimeIt.Common.Exporter;
using TimeIt.Common.Results;

namespace TimeIt;

public class ConsoleExporter : IExporter
{
    private Config? _configuration;

    public string Name => nameof(ConsoleExporter);

    public bool Enabled => true;

    public void SetConfiguration(Config configuration)
    {
        _configuration = configuration;
    }

    public void Export(IEnumerable<ScenarioResult> results)
    {
        if (_configuration is null)
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
        for (var i = 0; i < _configuration.Count; i++)
        {
            resultsTable.AddRow(results.Select(r => Utils.FromNanosecondsToMilliseconds(r.Durations[i]) + "ms").ToArray());
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
            "[dodgerblue1 bold]Mean[/]",
            "[dodgerblue1 bold]StdDev[/]",
            "[dodgerblue1 bold]StdErr[/]",
            "[dodgerblue1 bold]P99[/]",
            "[dodgerblue1 bold]P95[/]",
            "[dodgerblue1 bold]P90[/]",
            "[dodgerblue1 bold]Outliers[/]");

        // Add rows
        foreach (var result in results)
        {
            var totalNum = result.MetricsData.Count;

            if (totalNum > 0)
            {
                summaryTable.AddRow(
                    $"[aqua]{result.Name}[/]",
                    $"[aqua]{Utils.FromNanosecondsToMilliseconds(result.Mean)}ms[/]",
                    $"[aqua]{Utils.FromNanosecondsToMilliseconds(result.Stdev)}ms[/]",
                    $"[aqua]{Utils.FromNanosecondsToMilliseconds(result.StdErr)}ms[/]",
                    $"[aqua]{Utils.FromNanosecondsToMilliseconds(result.P99)}ms[/]",
                    $"[aqua]{Utils.FromNanosecondsToMilliseconds(result.P95)}ms[/]",
                    $"[aqua]{Utils.FromNanosecondsToMilliseconds(result.P90)}ms[/]",
                    $"[aqua]{result.Outliers.Count}[/]");

                var orderedMetricsData = result.MetricsData.OrderBy(item => item.Key).ToList();
                for (var i = 0; i < totalNum; i++)
                {
                    var item = orderedMetricsData[i];
                    var mMean = item.Value.Mean();
                    var mStdDev = item.Value.StandardDeviation();
                    var mStdErr = mStdDev / Math.Sqrt(item.Value.Count);
                    var mP99 = item.Value.Percentile(99);
                    var mP95 = item.Value.Percentile(95);
                    var mP90 = item.Value.Percentile(90);

                    string name;
                    if (i < totalNum - 1)
                    {
                        name = "├>" + item.Key;
                    }
                    else
                    {
                        name = "└>" + item.Key;
                    }
                    
                    summaryTable.AddRow(
                        name,
                        Math.Round(mMean, 6).ToString(),
                        Math.Round(mStdDev, 6).ToString(),
                        Math.Round(mStdErr, 6).ToString(),
                        Math.Round(mP99, 6).ToString(),
                        Math.Round(mP95, 6).ToString(),
                        Math.Round(mP90, 6).ToString(),
                        string.Empty);
                }

                summaryTable.AddRow(
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty);
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
    }
}
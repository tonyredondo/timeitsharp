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
            summaryTable.AddRow(
                result.Name,
                Utils.FromNanosecondsToMilliseconds(result.Mean) + "ms",
                Utils.FromNanosecondsToMilliseconds(result.Stdev) + "ms",
                Utils.FromNanosecondsToMilliseconds(result.StdErr) + "ms",
                Utils.FromNanosecondsToMilliseconds(result.P99) + "ms",
                Utils.FromNanosecondsToMilliseconds(result.P95) + "ms",
                Utils.FromNanosecondsToMilliseconds(result.P90) + "ms",
                result.Outliers.Count.ToString());
            
/*
 *
		totalNum := len(resScenario[scidx].MetricsData)
		if totalNum > 0 {
			for idx, item := range orderByKey(resScenario[scidx].MetricsData) {
				mMean, _ := stats.Mean(item.value)
				mStdDev, _ := stats.StandardDeviation(item.value)
				mStdErr := mStdDev / math.Sqrt(float64(len(resScenario[scidx].DataFloat)))
				mP99, _ := stats.Percentile(item.value, 99)
				mP95, _ := stats.Percentile(item.value, 95)
				mP90, _ := stats.Percentile(item.value, 90)

				var name string
				if idx < totalNum-1 {
					name = fmt.Sprintf("├>%v", item.key)
				} else {
					name = fmt.Sprintf("└>%v", item.key)
				}

				summaryTable.Append([]string{
					name,
					fmt.Sprint(toFixed(mMean, 6)),
					fmt.Sprint(toFixed(mStdDev, 6)),
					fmt.Sprint(toFixed(mStdErr, 6)),
					fmt.Sprint(toFixed(mP99, 6)),
					fmt.Sprint(toFixed(mP95, 6)),
					fmt.Sprint(toFixed(mP90, 6)),
					"",
				})
			}

			summaryTable.Append([]string{"", "", "", "", "", "", "", ""})
		}
 * 
 */
        }

        // Write table
        AnsiConsole.Write(summaryTable);
        AnsiConsole.WriteLine();
    }
}
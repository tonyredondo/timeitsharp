using Spectre.Console;
using TimeItSharp.Common;
using System.CommandLine;
using System.Text.Json;
using TimeItSharp.Common.Configuration;
using TimeItSharp.Common.Configuration.Builder;
using TimeItSharp.Common.Exporters;

var version = typeof(Program).Assembly.GetName().Version!;
AnsiConsole.MarkupLine("[bold dodgerblue1 underline]TimeItSharp v{0}[/]", $"{version.Major}.{version.Minor}.{version.Build}");

var argument = new Argument<string>("configuration file or process name", "The JSON configuration file or process name");
var templateVariables = new Option<TemplateVariables>(
    "--variable",
    isDefault: true,
    description: "Variables used to instantiate the configuration file",
    parseArgument: result =>
    {
        var tvs = new TemplateVariables();

        foreach (var token in result.Tokens)
        {
            var variableValue = token.Value; ;
            var idx = variableValue.IndexOf('=');
            if (idx == -1)
            {
                AnsiConsole.MarkupLine("[bold red]Unknown format: variable must be of the form[/][bold blue] key=value[/]");
                continue;
            }

            if (idx == variableValue.Length - 1)
            {
                AnsiConsole.MarkupLine("[bold red]No variable value provided. Skipped.[/]");
                continue;
            }

            if (idx == 0)
            {
                AnsiConsole.MarkupLine("[bold red]No variable name provided. Skipped.[/]");
                continue;
            }

            var keyVal = variableValue.Split('=');
            tvs.Add(keyVal[0], keyVal[1]);
        }
        return tvs;

    }) { Arity = ArgumentArity.OneOrMore };
var count = new Option<int?>("--count", "Number of iterations to run");
var warmup = new Option<int?>("--warmup", "Number of iterations to warm up");
var jsonExporter = new Option<bool>("--json-exporter", () => false, "Enable JSON exporter");
var datadogExporter = new Option<bool>("--datadog-exporter", () => false, "Enable Datadog exporter");

var root = new RootCommand
{
    argument,
    templateVariables,
    count,
    warmup,
    jsonExporter,
    datadogExporter
};

root.SetHandler(async (configFile, templateVariables, countValue, warmupValue, jsonExporterValue, datadogExporterValue) =>
{
    var isConfigFile = false;
    if (File.Exists(configFile))
    {
        try
        {
            await using var fstream = File.Open(configFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var config = JsonSerializer.Deserialize<Config>(fstream);
            isConfigFile = config is not null;
        }
        catch
        {
            // .
        }
    }
    else
    {
        AnsiConsole.MarkupLine("Configuration file not found, trying to run as a process name...");
    }

    var exitCode = 0;
    if (isConfigFile)
    {
        var config = Config.LoadConfiguration(configFile);
        config.WarmUpCount = warmupValue ?? config.WarmUpCount;
        config.Count = countValue ?? config.Count;
        var configBuilder = new ConfigBuilder(config);
        if (jsonExporterValue)
        {
            configBuilder.WithExporter<JsonExporter>();
        }

        if (datadogExporterValue)
        {
            configBuilder.WithExporter<DatadogExporter>();
        }

        exitCode = await TimeItEngine.RunAsync(configBuilder, new TimeItOptions(templateVariables)).ConfigureAwait(false);
    }
    else
    {
        var commandLineArray = configFile.Split(' ', StringSplitOptions.None);
        var processName = commandLineArray[0];
        var processArgs = string.Empty;
        if (commandLineArray.Length > 1)
        {
            processArgs = string.Join(' ', commandLineArray.Skip(1));
        }

        var configBuilder = ConfigBuilder.Create()
            .WithName(configFile)
            .WithProcessName(processName)
            .WithProcessArguments(processArgs)
            .WithMetrics(true)
            .WithWarmupCount(warmupValue ?? 1)
            .WithCount(countValue ?? 10)
            .WithExporter<ConsoleExporter>()
            .WithTimeout(t => t.WithMaxDuration((int)TimeSpan.FromMinutes(30).TotalSeconds))
            .WithScenario(s => s.WithName("Default"));

        if (jsonExporterValue)
        {
            configBuilder.WithExporter<JsonExporter>();
        }

        if (datadogExporterValue)
        {
            configBuilder.WithExporter<DatadogExporter>();
        }

        exitCode = await TimeItEngine.RunAsync(configBuilder, new TimeItOptions(templateVariables)).ConfigureAwait(false);
    }
    
    if (exitCode != 0)
    {
        Environment.Exit(exitCode);
    }
}, argument, templateVariables, count, warmup, jsonExporter, datadogExporter);

await root.InvokeAsync(args);

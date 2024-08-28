using Spectre.Console;
using TimeItSharp.Common;
using System.CommandLine;
using System.CommandLine.Binding;
using System.CommandLine.Invocation;
using System.Text.Json;
using TimeItSharp.Common.Configuration;
using TimeItSharp.Common.Configuration.Builder;
using TimeItSharp.Common.Exporters;
using TimeItSharp.Common.Services;

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
var metrics = new Option<bool>("--metrics", () => true, "Enable Metrics from startup hook");
var jsonExporter = new Option<bool>("--json-exporter", () => false, "Enable JSON exporter");
var datadogExporter = new Option<bool>("--datadog-exporter", () => false, "Enable Datadog exporter");
var datadogProfiler = new Option<bool>("--datadog-profiler", () => false, "Enable Datadog profiler");
var showStdOutForFistRun = new Option<bool>("--first-run-stdout", () => false, "Show the StdOut and StdErr for the first run");
var processFailedExecutions = new Option<bool>("--process-failed-executions", () => false, "Include failed executions in the final results");
var debugMode = new Option<bool>("--debug", () => false, "Run timeit in debug mode");

var root = new RootCommand
{
    argument,
    templateVariables,
    count,
    warmup,
    metrics,
    jsonExporter,
    datadogExporter,
    datadogProfiler,
    showStdOutForFistRun,
    processFailedExecutions,
    debugMode,
};

root.SetHandler(async (context) =>
{
    var argumentValue = GetValueForHandlerParameter(argument, context) ?? string.Empty;
    var templateVariablesValue = GetValueForHandlerParameter(templateVariables, context);
    var countValue = GetValueForHandlerParameter(count, context);
    var warmupValue = GetValueForHandlerParameter(warmup, context);
    var metricsValue = GetValueForHandlerParameter(metrics, context);
    var jsonExporterValue = GetValueForHandlerParameter(jsonExporter, context);
    var datadogExporterValue = GetValueForHandlerParameter(datadogExporter, context);
    var datadogProfilerValue = GetValueForHandlerParameter(datadogProfiler, context);
    var showStdOutForFistRunValue = GetValueForHandlerParameter(showStdOutForFistRun, context);
    var processFailedExecutionsValue = GetValueForHandlerParameter(processFailedExecutions, context);
    var debugModeValue = GetValueForHandlerParameter(debugMode, context);
    
    var isConfigFile = false;
    if (File.Exists(argumentValue))
    {
        try
        {
            await using var fstream = File.Open(argumentValue, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var config = JsonSerializer.Deserialize(fstream, ConfigContext.Default.Config);
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
        var config = Config.LoadConfiguration(argumentValue);
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

        exitCode = await TimeItEngine.RunAsync(configBuilder, new TimeItOptions(templateVariablesValue)).ConfigureAwait(false);
    }
    else
    {
        var commandLineArray = argumentValue.Split(' ', StringSplitOptions.None);
        var processName = commandLineArray[0];
        var processArgs = string.Empty;
        if (commandLineArray.Length > 1)
        {
            processArgs = string.Join(' ', commandLineArray.Skip(1));
        }

        var finalCount = countValue ?? 10;
        var configBuilder = ConfigBuilder.Create()
            .WithName(argumentValue)
            .WithProcessName(processName)
            .WithProcessArguments(processArgs)
            .WithMetrics(metricsValue)
            .WithWarmupCount(warmupValue ?? 1)
            .WithCount(finalCount)
            .WithExporter<ConsoleExporter>()
            .WithTimeout(t => t.WithMaxDuration((int)TimeSpan.FromMinutes(30).TotalSeconds))
            .WithScenario(s => s.WithName("Default"));

        if (showStdOutForFistRunValue)
        {
            configBuilder = configBuilder.ShowStdOutForFirstRun();
        }

        if (processFailedExecutionsValue)
        {
            configBuilder = configBuilder.ProcessFailedDataPoints();
        }

        if (debugModeValue)
        {
            configBuilder = configBuilder.WithDebugMode();
        }

        var timeitOption = new TimeItOptions(templateVariablesValue);

        if (jsonExporterValue)
        {
            configBuilder.WithExporter<JsonExporter>();
        }

        if (datadogExporterValue)
        {
            configBuilder.WithExporter<DatadogExporter>();
        }

        if (datadogProfilerValue)
        {
            configBuilder.WithService<DatadogProfilerService>();
            timeitOption = timeitOption.AddServiceState<DatadogProfilerService>(
                new DatadogProfilerConfiguration().WithExtraRun(finalCount * 40 / 100));
        }

        exitCode = await TimeItEngine.RunAsync(configBuilder, timeitOption).ConfigureAwait(false);
    }
    
    if (exitCode != 0)
    {
        Environment.Exit(exitCode);
    }
});

await root.InvokeAsync(args);

static T? GetValueForHandlerParameter<T>(
    IValueDescriptor<T> symbol,
    InvocationContext context)
{
    return symbol switch
    {
        IValueSource valueSource when valueSource.TryGetValue(symbol, context.BindingContext, out var boundValue) &&
                                      boundValue is T value => value,
        Argument argument => (T?)context.ParseResult.GetValueForArgument(argument),
        Option option => (T?)context.ParseResult.GetValueForOption(option),
        _ => default
    };
}

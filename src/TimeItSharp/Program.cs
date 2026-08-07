using Spectre.Console;
using TimeItSharp.Common;
using System.CommandLine;
using System.CommandLine.Binding;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using TimeItSharp.Cli;
using TimeItSharp.Common.Configuration;
using TimeItSharp.Common.Configuration.Builder;
using TimeItSharp.Common.Exporters;
using TimeItSharp.Common.Services;

var version = typeof(Program).Assembly.GetName().Version!;
AnsiConsole.MarkupLine("[bold dodgerblue1 underline]TimeItSharp v{0}[/]", $"{version.Major}.{version.Minor}.{version.Build}");

var argument = new Argument<string[]>("configuration file or process name", "The JSON configuration file or executable command")
{
    Arity = ArgumentArity.ZeroOrMore,
};
var configurationPath = new Option<string?>("--config", "Explicitly select a JSON configuration file");
var command = new Option<string?>("--command", "Explicitly select a process command");
var templateVariables = new Option<TemplateVariables>(
    "--variable",
    isDefault: true,
    description: "Variables used to instantiate the configuration file",
    parseArgument: result =>
    {
        var tvs = new TemplateVariables();

        foreach (var token in result.Tokens)
        {
            var variableValue = token.Value;
            var idx = variableValue.IndexOf('=');
            if (idx == -1)
            {
                AnsiConsole.MarkupLine("[bold red]Unknown format: variable must be of the form[/][bold blue] key=value[/]");
                continue;
            }

            if (idx == 0)
            {
                AnsiConsole.MarkupLine("[bold red]No variable name provided. Skipped.[/]");
                continue;
            }

            // Split only at the first equals sign. Values such as connection strings and
            // base64 payloads commonly contain additional equals signs.
            var key = variableValue[..idx].Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                AnsiConsole.MarkupLine("[bold red]No variable name provided. Skipped.[/]");
                continue;
            }

            var value = variableValue[(idx + 1)..];
            tvs.Add(key, value);
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
    configurationPath,
    command,
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
    var positionalArguments = GetValueForHandlerParameter(argument, context) ?? Array.Empty<string>();
    var positionalArgument = CliInputParser.JoinCommandArguments(positionalArguments);
    var configurationPathValue = GetValueForHandlerParameter(configurationPath, context);
    var commandValue = GetValueForHandlerParameter(command, context);
    if (commandValue is not null && positionalArguments.Length != 0)
    {
        // Values before a standalone `--` are the command's executable/leading arguments;
        // terminated values follow them. Appending in the opposite order turns
        // `echo -- --config foo.json` into `--config foo.json echo`.
        commandValue = string.IsNullOrEmpty(commandValue)
            ? positionalArgument
            : $"{positionalArgument} {commandValue}";
    }

    CliInput cliInput;
    try
    {
        if (configurationPathValue is not null && positionalArguments.Length != 0)
        {
            throw new ArgumentException("--config cannot be combined with a positional command.");
        }

        cliInput = CliInputParser.Classify(positionalArgument, configurationPathValue, commandValue);
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine("[red]An error occurred while parsing the TimeItSharp input:[/]");
        AnsiConsole.WriteException(Utils.SanitizeException(ex,
            new[] { positionalArgument, configurationPathValue, commandValue }.OfType<string>()));
        Environment.ExitCode = 1;
        return;
    }

    var argumentValue = cliInput.Value;
    var inputRedactionValues = new[] { argumentValue, configurationPathValue, commandValue }
        .OfType<string>()
        .Where(value => value.Length > 0)
        .ToArray();
    var templateVariablesValue = GetValueForHandlerParameter(templateVariables, context) ?? new TemplateVariables();
    var countValue = GetValueForHandlerParameter(count, context);
    var warmupValue = GetValueForHandlerParameter(warmup, context);
    var metricsValue = GetValueForHandlerParameter(metrics, context);
    var jsonExporterValue = GetValueForHandlerParameter(jsonExporter, context);
    var datadogExporterValue = GetValueForHandlerParameter(datadogExporter, context);
    var datadogProfilerValue = GetValueForHandlerParameter(datadogProfiler, context);
    var showStdOutForFistRunValue = GetValueForHandlerParameter(showStdOutForFistRun, context);
    var processFailedExecutionsValue = GetValueForHandlerParameter(processFailedExecutions, context);
    var debugModeValue = GetValueForHandlerParameter(debugMode, context);

    // Bool options have defaults, so inspect the parse result as well. This lets a JSON
    // configuration keep an explicitly configured value unless the corresponding CLI flag
    // was supplied, while still allowing e.g. --metrics false to override it.
    var metricsSpecified = IsOptionSpecified(metrics, context);
    var showStdOutForFirstRunSpecified = IsOptionSpecified(showStdOutForFistRun, context);
    var processFailedExecutionsSpecified = IsOptionSpecified(processFailedExecutions, context);
    var debugModeSpecified = IsOptionSpecified(debugMode, context);
    var jsonExporterSpecified = IsOptionSpecified(jsonExporter, context);
    var datadogExporterSpecified = IsOptionSpecified(datadogExporter, context);
    var datadogProfilerSpecified = IsOptionSpecified(datadogProfiler, context);

    Config? loadedConfig = null;
    Exception? configurationLoadError = null;
    var fileExists = File.Exists(argumentValue);
    var isConfigurationFile = cliInput.IsConfiguration;
    if (isConfigurationFile)
    {
        try
        {
            loadedConfig = Config.LoadConfiguration(argumentValue);
        }
        catch (Exception ex)
        {
            // A configuration-looking file must not silently be interpreted as an executable
            // command when it is malformed or inaccessible. Register the requested path before
            // preserving the exception because filesystem messages often echo it.
            configurationLoadError = Utils.SanitizeException(ex, inputRedactionValues);
        }
    }
    else if (!fileExists && !cliInput.IsExplicit)
    {
        AnsiConsole.MarkupLine("Configuration file not found, trying to run as a process name...");
    }

    var exitCode = 0;

    try
    {
        if (configurationLoadError is not null)
        {
            throw configurationLoadError;
        }

        if (loadedConfig is not null)
        {
            var configBuilder = new ConfigBuilder(loadedConfig);
            if (warmupValue.HasValue)
            {
                configBuilder.Build().WarmUpCount = warmupValue.Value;
            }

            if (countValue.HasValue)
            {
                configBuilder.Build().Count = countValue.Value;
            }

            if (metricsSpecified)
            {
                configBuilder.WithMetrics(metricsValue);
            }

            // These options are enabling flags for the command mode, but assigning the value
            // when explicitly supplied also supports --debug false on a JSON configuration.
            if (showStdOutForFirstRunSpecified)
            {
                configBuilder.Build().ShowStdOutForFirstRun = showStdOutForFistRunValue;
            }

            if (processFailedExecutionsSpecified)
            {
                configBuilder.Build().ProcessFailedDataPoints = processFailedExecutionsValue;
            }

            if (debugModeSpecified)
            {
                configBuilder.Build().DebugMode = debugModeValue;
            }

            // A non-empty exporter list is an explicit replacement for the built-in defaults.
            // If an exporter flag is supplied while the list is empty, materialize those defaults
            // first so true/false overrides behave symmetrically.
            if (jsonExporterSpecified)
            {
                EnsureDefaultExporters(configBuilder);
                if (jsonExporterValue == true)
                {
                    configBuilder.WithExporter<JsonExporter>();
                }
                else
                {
                    RemoveExporter(configBuilder.Build(), typeof(JsonExporter), "Json", "JsonExporter");
                    EnsureAtLeastOneExporter(configBuilder);
                }
            }

            if (datadogExporterSpecified)
            {
                EnsureDefaultExporters(configBuilder);
                configBuilder.Build().EnableDatadog = datadogExporterValue;
                if (datadogExporterValue == true)
                {
                    configBuilder.WithExporter<DatadogExporter>();
                }
                else
                {
                    RemoveExporter(configBuilder.Build(), typeof(DatadogExporter), "Datadog", "DatadogExporter");
                    EnsureAtLeastOneExporter(configBuilder);
                }
            }

            var timeitOptions = new TimeItOptions(templateVariablesValue);
            if (datadogProfilerSpecified)
            {
                if (datadogProfilerValue == true)
                {
                    configBuilder.WithService<DatadogProfilerService>();
                    var finalCount = countValue ?? configBuilder.Build().Count;
                    var extraRunCount = (int)Math.Min((long)finalCount * 40 / 100, int.MaxValue);
                    timeitOptions = timeitOptions.AddServiceState<DatadogProfilerService>(
                        new DatadogProfilerConfiguration().WithExtraRun(extraRunCount));
                }
                else
                {
                    RemoveService(configBuilder.Build(), typeof(DatadogProfilerService),
                        "DatadogProfiler", "DatadogProfilerService");
                }
            }

            // Validate after applying overrides so malformed CLI values are reported before any
            // extension is loaded or process is started. The engine validates again for library
            // callers that do not go through this CLI.
            configBuilder.Build().Validate();
            exitCode = await TimeItEngine.RunAsync(configBuilder, timeitOptions).ConfigureAwait(false);
        }
        else
        {
            var processCommand = CliInputParser.ParseProcessCommand(argumentValue);
            var processName = processCommand.ProcessName;
            var processArgs = processCommand.ProcessArguments;
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

            if (showStdOutForFistRunValue == true)
            {
                configBuilder.ShowStdOutForFirstRun();
            }

            if (processFailedExecutionsValue == true)
            {
                configBuilder.ProcessFailedDataPoints();
            }

            if (debugModeValue == true)
            {
                configBuilder.WithDebugMode();
            }

            var timeitOptions = new TimeItOptions(templateVariablesValue);
            if (jsonExporterValue == true)
            {
                configBuilder.WithExporter<JsonExporter>();
            }

            if (datadogExporterValue == true)
            {
                configBuilder.Build().EnableDatadog = true;
                configBuilder.WithExporter<DatadogExporter>();
            }

            if (datadogProfilerValue == true)
            {
                configBuilder.WithService<DatadogProfilerService>();
                var extraRunCount = (int)Math.Min((long)finalCount * 40 / 100, int.MaxValue);
                timeitOptions = timeitOptions.AddServiceState<DatadogProfilerService>(
                    new DatadogProfilerConfiguration().WithExtraRun(extraRunCount));
            }

            configBuilder.Build().Validate();
            exitCode = await TimeItEngine.RunAsync(configBuilder, timeitOptions).ConfigureAwait(false);
        }
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine("[red]An error occurred while running TimeItSharp:[/]");
        AnsiConsole.WriteException(Utils.SanitizeException(ex, inputRedactionValues));
        exitCode = 1;
    }

    Environment.ExitCode = exitCode;
});

var normalizedArguments = CliInputParser.NormalizeArguments(args);
var invocationExitCode = await root.InvokeAsync(normalizedArguments);
if (Environment.ExitCode == 0)
{
    Environment.ExitCode = invocationExitCode;
}

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

static bool IsOptionSpecified<T>(Option<T> option, InvocationContext context)
{
    foreach (var child in context.ParseResult.RootCommandResult.Children)
    {
        if (child is OptionResult result && result.Option == option && !result.IsImplicit)
        {
            return true;
        }
    }

    return false;
}

static void EnsureDefaultExporters(ConfigBuilder configBuilder)
{
    var configuration = configBuilder.Build();
    if (configuration.Exporters is null || configuration.Exporters.Count != 0)
    {
        return;
    }

    // Materialize only the non-Datadog defaults unless the configuration explicitly enabled
    // Datadog. A declaration itself is now an enablement signal, so adding it unconditionally
    // would make --json-exporter true/false unexpectedly start CI Visibility.
    var datadogEnabled = configuration.EnableDatadog;
    configBuilder
        .WithExporter<ConsoleExporter>()
        .WithExporter<JsonExporter>();
    if (datadogEnabled)
    {
        configBuilder.WithExporter<DatadogExporter>();
    }

    configuration.EnableDatadog = datadogEnabled;
}

static void EnsureAtLeastOneExporter(ConfigBuilder configBuilder)
{
    var configuration = configBuilder.Build();
    if (configuration.Exporters is { Count: 0 })
    {
        configBuilder.WithExporter<ConsoleExporter>();
    }
}

static void RemoveExporter(Config configuration, Type exporterType, params string[] names)
{
    configuration.Exporters?.RemoveAll(info => IsExtension(info, exporterType, names));
}

static void RemoveService(Config configuration, Type serviceType, params string[] names)
{
    configuration.Services?.RemoveAll(info => IsExtension(info, serviceType, names));
}

static bool IsExtension(AssemblyLoadInfo? info, Type extensionType, IReadOnlyCollection<string> names)
{
    return info is not null &&
           (info.InMemoryType == extensionType ||
            string.Equals(info.Type, extensionType.FullName, StringComparison.Ordinal) ||
            (info.Name is not null && names.Contains(info.Name, StringComparer.OrdinalIgnoreCase)));
}

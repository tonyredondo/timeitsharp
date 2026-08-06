using Spectre.Console;
using TimeItSharp.Common;
using System.CommandLine;
using System.CommandLine.Binding;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Text;
using TimeItSharp.Common.Configuration;
using TimeItSharp.Common.Configuration.Builder;
using TimeItSharp.Common.Exporters;
using TimeItSharp.Common.Services;

var version = typeof(Program).Assembly.GetName().Version!;
AnsiConsole.MarkupLine("[bold dodgerblue1 underline]TimeItSharp v{0}[/]", $"{version.Major}.{version.Minor}.{version.Build}");

var argument = new Argument<string>("configuration file or process name", "The JSON configuration file or executable command");
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
    var datadogExporterSpecified = IsOptionSpecified(datadogExporter, context);

    Config? loadedConfig = null;
    Exception? configurationLoadError = null;
    var fileExists = File.Exists(argumentValue);
    var hasJsonExtension = string.Equals(Path.GetExtension(argumentValue), ".json", StringComparison.OrdinalIgnoreCase);
    var isConfigurationFile = hasJsonExtension || (fileExists && LooksLikeJsonConfiguration(argumentValue));
    if (isConfigurationFile)
    {
        try
        {
            loadedConfig = Config.LoadConfiguration(argumentValue);
        }
        catch (Exception ex)
        {
            // A configuration-looking file must not silently be interpreted as an executable
            // command when it is malformed or inaccessible.
            configurationLoadError = ex;
        }
    }
    else if (!fileExists)
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

            if (jsonExporterValue == true)
            {
                configBuilder.WithExporter<JsonExporter>();
            }

            if (datadogExporterSpecified)
            {
                configBuilder.Build().EnableDatadog = datadogExporterValue;
                if (datadogExporterValue == true)
                {
                    configBuilder.WithExporter<DatadogExporter>();
                }
            }

            var timeitOptions = new TimeItOptions(templateVariablesValue);
            if (datadogProfilerValue == true)
            {
                configBuilder.WithService<DatadogProfilerService>();
                var finalCount = countValue ?? configBuilder.Build().Count;
                var extraRunCount = (int)Math.Min((long)finalCount * 40 / 100, int.MaxValue);
                timeitOptions = timeitOptions.AddServiceState<DatadogProfilerService>(
                    new DatadogProfilerConfiguration().WithExtraRun(extraRunCount));
            }

            // Validate after applying overrides so malformed CLI values are reported before any
            // extension is loaded or process is started. The engine validates again for library
            // callers that do not go through this CLI.
            configBuilder.Build().Validate();
            exitCode = await TimeItEngine.RunAsync(configBuilder, timeitOptions).ConfigureAwait(false);
        }
        else
        {
            var (processName, processArgs) = ParseProcessCommand(argumentValue);
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
        AnsiConsole.WriteLine(ex.ToString());
        exitCode = 1;
    }

    Environment.ExitCode = exitCode;
});

var invocationExitCode = await root.InvokeAsync(args);
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

static (string ProcessName, string ProcessArguments) ParseProcessCommand(string commandLine)
{
    if (string.IsNullOrWhiteSpace(commandLine))
    {
        throw new ArgumentException("A process name or command is required.", nameof(commandLine));
    }

    var index = 0;
    while (index < commandLine.Length && char.IsWhiteSpace(commandLine[index]))
    {
        index++;
    }

    var processName = new StringBuilder();
    char quote = '\0';
    while (index < commandLine.Length)
    {
        var current = commandLine[index];
        if (quote != '\0')
        {
            if (current == quote)
            {
                quote = '\0';
                index++;
                continue;
            }

            // Preserve normal backslashes (important for Windows paths), while accepting the
            // conventional escaped quote and escaped backslash forms.
            if (current == '\\' && index + 1 < commandLine.Length &&
                (commandLine[index + 1] == quote || commandLine[index + 1] == '\\'))
            {
                processName.Append(commandLine[index + 1]);
                index += 2;
                continue;
            }

            processName.Append(current);
            index++;
            continue;
        }

        if (char.IsWhiteSpace(current))
        {
            break;
        }

        if (current is '\'' or '"')
        {
            quote = current;
            index++;
            continue;
        }

        if (current == '\\' && index + 1 < commandLine.Length &&
            (commandLine[index + 1] is '\'' or '"'))
        {
            processName.Append(commandLine[index + 1]);
            index += 2;
            continue;
        }

        processName.Append(current);
        index++;
    }

    if (quote != '\0')
    {
        throw new ArgumentException("The process command contains an unterminated quote.", nameof(commandLine));
    }

    if (processName.Length == 0)
    {
        throw new ArgumentException("A process name is required.", nameof(commandLine));
    }

    while (index < commandLine.Length && char.IsWhiteSpace(commandLine[index]))
    {
        index++;
    }

    var processArguments = index < commandLine.Length ? commandLine[index..].TrimEnd() : string.Empty;
    EnsureBalancedQuotes(processArguments, commandLine);
    return (processName.ToString(), processArguments);
}

static bool LooksLikeJsonConfiguration(string filePath)
{
    try
    {
        using var stream = File.OpenRead(filePath);
        var buffer = new byte[4096];
        var read = stream.Read(buffer, 0, buffer.Length);
        var index = 0;
        if (read >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
        {
            index = 3;
        }

        while (index < read && char.IsWhiteSpace((char)buffer[index]))
        {
            index++;
        }

        return index < read && buffer[index] == (byte)'{';
    }
    catch (IOException)
    {
        return false;
    }
    catch (UnauthorizedAccessException)
    {
        return false;
    }
}

static void EnsureBalancedQuotes(string text, string commandLine)
{
    char quote = '\0';
    for (var index = 0; index < text.Length; index++)
    {
        var current = text[index];
        if (current == '\\' && quote == '"' && index + 1 < text.Length &&
            (text[index + 1] == quote || text[index + 1] == '\\'))
        {
            index++;
            continue;
        }

        if (quote == '\0' && current is '\'' or '"')
        {
            quote = current;
        }
        else if (quote != '\0' && current == quote)
        {
            quote = '\0';
        }
    }

    if (quote != '\0')
    {
        throw new ArgumentException("The process command contains an unterminated quote.", nameof(commandLine));
    }
}

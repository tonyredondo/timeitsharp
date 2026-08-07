using CliWrap;
using System.Text;
using Spectre.Console;
using TimeItSharp.Common.Results;

namespace TimeItSharp.Common.Services;

public sealed class ExecuteService : IService
{
    private static readonly TaskFactory _taskFactory = new(CancellationToken.None, TaskCreationOptions.None, TaskContinuationOptions.None, TaskScheduler.Default);
    private ExecuteConfiguration? _configuration = null;
    private IReadOnlyDictionary<string, string?> _hostEnvironment = new Dictionary<string, string?>();
    private IReadOnlyList<string> _knownSecretValues = Array.Empty<string>();
    private CancellationToken _cancellationToken;

    public string Name => "Execute";

    public void Initialize(InitOptions options, TimeItCallbacks callbacks)
    {
        if (options.State is ExecuteConfiguration configuration)
        {
            _configuration = configuration;
        }
        else
        {
            _configuration = new(options.LoadInfo?.Options);
        }

        ValidateConfiguration(_configuration);
        _cancellationToken = callbacks.CancellationToken;

        var hostEnvironment = options.HostEnvironment ?? ScenarioProcessor.CaptureEnvironmentVariables();
        _hostEnvironment = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string?>(
            new Dictionary<string, string?>(hostEnvironment,
                OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal));

        _knownSecretValues = Utils.GetSensitiveEnvironmentValues(options.Configuration?.EnvironmentVariables)
            .Concat(Utils.GetSensitiveEnvironmentSnapshotValues(options.HostEnvironment))
            .Concat(Utils.GetTemplateSecretValues(options.TemplateVariables))
            // Callback command values are user-controlled and may be opaque literals rather than
            // deny-listed names. Redact their parsed argv values before any callback output sink.
            .Concat(GetConfiguredCommandValues(_configuration, options.TemplateVariables))
            .Where(value => !string.IsNullOrEmpty(value) && value.Length <= Utils.MaxExportLogCharacters)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        
        if (_configuration.OnScenarioStart is { } onScenarioStart)
        {
            callbacks.OnScenarioStart += (scenario) =>
            {
                if (onScenarioStart.CreateCommand(options.TemplateVariables) is { } command)
                {
                    ExecuteCommand("OnScenarioStart", command, onScenarioStart.TimeoutInSeconds, onScenarioStart.RedirectStandardOutput);
                }
            };
        }
        
        if (_configuration.OnScenarioFinish is { } onScenarioFinish)
        {
            callbacks.OnScenarioFinish += (scenarioResult) =>
            {
                if (onScenarioFinish.CreateCommand(options.TemplateVariables) is { } command)
                {
                    ExecuteCommand("OnScenarioFinish", command, onScenarioFinish.TimeoutInSeconds, onScenarioFinish.RedirectStandardOutput);
                }
            };
        }
        
        if (_configuration.AfterAllScenariosFinishes is { } afterAllScenariosFinishes)
        {
            callbacks.AfterAllScenariosFinishes += (scenariosResults) =>
            {
                if (afterAllScenariosFinishes.CreateCommand(options.TemplateVariables) is { } command)
                {
                    ExecuteCommand("AfterAllScenariosFinishes", command, afterAllScenariosFinishes.TimeoutInSeconds, afterAllScenariosFinishes.RedirectStandardOutput);
                }
            };
        }
        
        if (_configuration.OnFinish is { } onFinish)
        {
            callbacks.OnFinish += () =>
            {
                if (onFinish.CreateCommand(options.TemplateVariables) is { } command)
                {
                    ExecuteCommand("OnFinish", command, onFinish.TimeoutInSeconds, onFinish.RedirectStandardOutput);
                }
            };
        }
        
        if (_configuration.OnExecutionStart is { } onExecutionStart)
        {
            callbacks.OnExecutionStart += (DataPoint dataPoint, TimeItPhase phase, ref Command cmd) =>
            {
                if (onExecutionStart.CreateCommand(options.TemplateVariables) is { } command)
                {
                    ExecuteCommand("OnExecutionStart", command, onExecutionStart.TimeoutInSeconds, onExecutionStart.RedirectStandardOutput);
                }
            };
        }
        
        if (_configuration.OnExecutionEnd is { } onExecutionEnd)
        {
            callbacks.OnExecutionEnd += (dataPoint, phase) =>
            {
                if (onExecutionEnd.CreateCommand(options.TemplateVariables) is { } command)
                {
                    ExecuteCommand("OnExecutionEnd", command, onExecutionEnd.TimeoutInSeconds, onExecutionEnd.RedirectStandardOutput);
                }
            };
        }
    }

    private static void ValidateConfiguration(ExecuteConfiguration configuration)
    {
        var processData = new (string Name, ExecuteConfiguration.ProcessData? Value)[]
        {
            (nameof(configuration.OnScenarioStart), configuration.OnScenarioStart),
            (nameof(configuration.OnScenarioFinish), configuration.OnScenarioFinish),
            (nameof(configuration.OnExecutionStart), configuration.OnExecutionStart),
            (nameof(configuration.OnExecutionEnd), configuration.OnExecutionEnd),
            (nameof(configuration.AfterAllScenariosFinishes), configuration.AfterAllScenariosFinishes),
            (nameof(configuration.OnFinish), configuration.OnFinish),
        };

        foreach (var item in processData)
        {
            item.Value?.Validate(item.Name);
        }
    }

    private static IEnumerable<string> GetConfiguredCommandValues(
        ExecuteConfiguration configuration,
        TemplateVariables templateVariables)
    {
        var processData = new[]
        {
            configuration.OnScenarioStart,
            configuration.OnScenarioFinish,
            configuration.OnExecutionStart,
            configuration.OnExecutionEnd,
            configuration.AfterAllScenariosFinishes,
            configuration.OnFinish,
        };

        foreach (var item in processData)
        {
            if (item is null)
            {
                continue;
            }

            foreach (var configuredValue in new[]
                     {
                         item.ProcessName,
                         item.ProcessArguments,
                         item.WorkingDirectory,
                     }.Where(value => !string.IsNullOrEmpty(value)))
            {
                yield return configuredValue!;
                var expandedValue = templateVariables.Expand(configuredValue!);
                if (!string.Equals(expandedValue, configuredValue, StringComparison.Ordinal))
                {
                    yield return expandedValue;
                }
            }

            if (string.IsNullOrEmpty(item.ProcessArguments))
            {
                continue;
            }

            IReadOnlyList<string> values;
            try
            {
                values = CommandLineArguments.Parse(templateVariables.Expand(item.ProcessArguments));
            }
            catch (ArgumentException)
            {
                continue;
            }

            foreach (var value in values.Where(value => !string.IsNullOrEmpty(value)))
            {
                yield return value;
            }
        }
    }

    private void ExecuteCommand(
        string optionName,
        Command command,
        int timeoutInSeconds,
        bool writeToStdOut = false)
    {
        try
        {
            // Callback processes receive the immutable run-entry environment snapshot rather than
            // whatever a custom extension may have changed in the host after initialization.
            command = command.WithEnvironmentVariables(new Dictionary<string, string?>(_hostEnvironment));
            using var timeoutCts = new CancellationTokenSource();
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutInSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                _cancellationToken,
                timeoutCts.Token);
            var (result, processId, standardOutput, standardError) =
                ExecuteCapturedSync(command, linkedCts.Token);
            if (writeToStdOut)
            {
                AnsiConsole.WriteLine(
                    "ExecuteService.{0}: ProcessId: {1}, ProcessName: {2}, Duration: {3}, ExitCode: {4}", optionName, processId,
                    Utils.SanitizeText(command.TargetFilePath, _knownSecretValues),
                    result.RunTime, result.ExitCode);
                WriteCapturedOutput(standardOutput);
                WriteCapturedOutput(standardError);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException)
        {
            while (ex.InnerException is not null)
            {
                ex = ex.InnerException;
            }

            AnsiConsole.WriteException(Utils.SanitizeException(ex, _knownSecretValues));
        }
    }

    private (CommandResult Result, int ProcessId, string StandardOutput, string StandardError) ExecuteCapturedSync(
        Command command, CancellationToken cancellationToken = default)
    {
        return _taskFactory
            .StartNew(() => ExecuteCapturedAsync(command, cancellationToken), cancellationToken)
            .Unwrap()
            .GetAwaiter()
            .GetResult();

        static async Task<(CommandResult Result, int ProcessId, string StandardOutput, string StandardError)> ExecuteCapturedAsync(
            Command command, CancellationToken cancellationToken)
        {
            var standardOutput = new BoundedOutputCollector();
            var standardError = new BoundedOutputCollector();
            var capturedCommand = command
                .WithStandardOutputPipe(PipeTarget.ToDelegate(standardOutput.AppendAsync))
                .WithStandardErrorPipe(PipeTarget.ToDelegate(standardError.AppendAsync));
            var commandTask = capturedCommand.ExecuteAsync(cancellationToken);
            var processId = commandTask.ProcessId;
            var result = await commandTask.ConfigureAwait(false);
            return (result, processId, standardOutput.GetText(), standardError.GetText());
        }
    }

    private void WriteCapturedOutput(string output)
    {
        if (!string.IsNullOrEmpty(output))
        {
            AnsiConsole.WriteLine(Utils.SanitizeOutput(output, _knownSecretValues));
        }
    }

    private sealed class BoundedOutputCollector
    {
        private const int MaximumCharacters = 256 * 1024;
        private readonly object _gate = new();
        private readonly StringBuilder _builder = new();
        private int _bytes;
        private bool _truncated;

        public Task AppendAsync(string chunk, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                if (_truncated || string.IsNullOrEmpty(chunk))
                {
                    return Task.CompletedTask;
                }

                var remaining = MaximumCharacters - _bytes;
                if (remaining <= 0)
                {
                    _truncated = true;
                    return Task.CompletedTask;
                }

                var count = Math.Min(remaining, chunk.Length);
                _builder.Append(chunk.AsSpan(0, count));
                _bytes += count;
                if (count < chunk.Length)
                {
                    _truncated = true;
                }
            }

            return Task.CompletedTask;
        }

        public string GetText()
        {
            lock (_gate)
            {
                return _truncated
                    ? _builder + Environment.NewLine + "[OUTPUT TRUNCATED]"
                    : _builder.ToString();
            }
        }
    }

    public object? GetExecutionServiceData() => null;

    public object? GetScenarioServiceData() => null;
}
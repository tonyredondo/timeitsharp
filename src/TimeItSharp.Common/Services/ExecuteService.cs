using CliWrap;
using CliWrap.Buffered;
using Spectre.Console;
using TimeItSharp.Common.Results;

namespace TimeItSharp.Common.Services;

public sealed class ExecuteService : IService
{
    private static readonly TaskFactory _taskFactory = new(CancellationToken.None, TaskCreationOptions.None, TaskContinuationOptions.None, TaskScheduler.Default);
    private ExecuteConfiguration? _configuration = null;

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
        
        if (_configuration.OnScenarioStart is { } onScenarioStart)
        {
            callbacks.OnScenarioStart += (scenario) =>
            {
                if (onScenarioStart.CreateCommand(options.TemplateVariables) is { } command)
                {
                    ExecuteCommand("OnScenarioStart", command, onScenarioStart.RedirectStandardOutput);
                }
            };
        }
        
        if (_configuration.OnScenarioFinish is { } onScenarioFinish)
        {
            callbacks.OnScenarioFinish += (scenarioResult) =>
            {
                if (onScenarioFinish.CreateCommand(options.TemplateVariables) is { } command)
                {
                    ExecuteCommand("OnScenarioFinish", command, onScenarioFinish.RedirectStandardOutput);
                }
            };
        }
        
        if (_configuration.AfterAllScenariosFinishes is { } afterAllScenariosFinishes)
        {
            callbacks.AfterAllScenariosFinishes += (scenariosResults) =>
            {
                if (afterAllScenariosFinishes.CreateCommand(options.TemplateVariables) is { } command)
                {
                    ExecuteCommand("AfterAllScenariosFinishes", command, afterAllScenariosFinishes.RedirectStandardOutput);
                }
            };
        }
        
        if (_configuration.OnFinish is { } onFinish)
        {
            callbacks.OnFinish += () =>
            {
                if (onFinish.CreateCommand(options.TemplateVariables) is { } command)
                {
                    ExecuteCommand("OnFinish", command, onFinish.RedirectStandardOutput);
                }
            };
        }
        
        if (_configuration.OnExecutionStart is { } onExecutionStart)
        {
            callbacks.OnExecutionStart += (DataPoint dataPoint, TimeItPhase phase, ref Command cmd) =>
            {
                if (onExecutionStart.CreateCommand(options.TemplateVariables) is { } command)
                {
                    ExecuteCommand("OnExecutionStart", command, onExecutionStart.RedirectStandardOutput);
                }
            };
        }
        
        if (_configuration.OnExecutionEnd is { } onExecutionEnd)
        {
            callbacks.OnExecutionEnd += (dataPoint, phase) =>
            {
                if (onExecutionEnd.CreateCommand(options.TemplateVariables) is { } command)
                {
                    ExecuteCommand("OnExecutionEnd", command, onExecutionEnd.RedirectStandardOutput);
                }
            };
        }
    }

    private static void ExecuteCommand(string optionName, Command command, bool writeToStdOut = false)
    {
        try
        {
            var (result, processId) = ExecuteBufferedSync(command);
            if (writeToStdOut)
            {
                AnsiConsole.WriteLine(
                    "ExecuteService.{0}: ProcessId: {1}, ProcessName: {2}, Duration: {3}, ExitCode: {4}", optionName, processId,
                    command.TargetFilePath,
                    result.RunTime, result.ExitCode);
            }
        }
        catch (Exception ex)
        {
            while (ex.InnerException is not null)
            {
                ex = ex.InnerException;
            }

            AnsiConsole.WriteLine(
                "ExecuteService.{0}: Error executing process: {1}", optionName, ex.Message);
        }
    }

    private static (BufferedCommandResult Result, int ProcessId) ExecuteBufferedSync(Command command, CancellationToken cancellationToken = default)
    {
        return _taskFactory
            .StartNew(() => ExecuteBufferedAsync(command, cancellationToken), cancellationToken)
            .Unwrap()
            .GetAwaiter()
            .GetResult();
        
        static async Task<(BufferedCommandResult Result, int ProcessId)> ExecuteBufferedAsync(Command command, CancellationToken cancellationToken = default)
        {
            var cmdtsk = command.ExecuteBufferedAsync(cancellationToken);
            var processId = cmdtsk.ProcessId;
            var result = await cmdtsk.ConfigureAwait(false);
            return (result, processId);
        }
    }

    public object? GetExecutionServiceData() => null;

    public object? GetScenarioServiceData() => null;
}
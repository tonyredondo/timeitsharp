using System.Text.Json;
using CliWrap;

namespace TimeItSharp.Common.Services;

public sealed class ExecuteConfiguration
{
    public ProcessData? OnScenarioStart { get; set; }
    public ProcessData? OnScenarioFinish { get; set; }
    public ProcessData? OnExecutionStart { get; set; }
    public ProcessData? OnExecutionEnd { get; set; }
    public ProcessData? AfterAllScenariosFinishes { get; set; }
    public ProcessData? OnFinish { get; set; }

    public ExecuteConfiguration(Dictionary<string, JsonElement?>? options = null)
    {
        if (options is not null)
        {
            OnScenarioStart = CreateProcessData("onScenarioStart", options);
            OnScenarioFinish = CreateProcessData("onScenarioFinish", options);
            OnExecutionStart = CreateProcessData("onExecutionStart", options);
            OnExecutionEnd = CreateProcessData("onExecutionEnd", options);
            AfterAllScenariosFinishes = CreateProcessData("afterAllScenariosFinishes", options);
            OnFinish = CreateProcessData("onFinish", options);
        }
    }

    private ProcessData? CreateProcessData(string optionName, Dictionary<string, JsonElement?> options)
    {
        if (options.TryGetValue(optionName, out var jsonElement) &&
            jsonElement is not null)
        {
            var processData = new ProcessData();
            if (jsonElement.Value.TryGetProperty("processName", out var processNameJsonElement))
            {
                processData.ProcessName = processNameJsonElement.GetString();
            }
            if (jsonElement.Value.TryGetProperty("processArguments", out var processArgumentsJsonElement))
            {
                processData.ProcessArguments = processArgumentsJsonElement.GetString();
            }
            if (jsonElement.Value.TryGetProperty("workingDirectory", out var workingDirectoryJsonElement))
            {
                processData.WorkingDirectory = workingDirectoryJsonElement.GetString();
            }
            if (jsonElement.Value.TryGetProperty("redirectStandardOutput", out var redirectStandardOutputJsonElement))
            {
                processData.RedirectStandardOutput = redirectStandardOutputJsonElement.GetBoolean();
            }
            if (jsonElement.Value.TryGetProperty("timeoutInSeconds", out var timeoutInSecondsJsonElement))
            {
                processData.TimeoutInSeconds = timeoutInSecondsJsonElement.GetInt32();
            }

            processData.Validate(optionName);
            return processData;
        }

        return null;
    }

    public class ProcessData
    {
        public string? ProcessName { get; set; }
        public string? ProcessArguments { get; set; }

        /// <summary>
        /// Maximum time allowed for this callback process. The default keeps existing
        /// configurations source-compatible while ensuring a callback cannot block the engine
        /// lifecycle indefinitely.
        /// </summary>
        public int TimeoutInSeconds { get; set; } = 300;

        public const int MaximumTimeoutInSeconds = 4_294_967;

        public string? WorkingDirectory { get; set; }
        public bool RedirectStandardOutput { get; set; }

        internal void Validate(string optionName)
        {
            if (TimeoutInSeconds <= 0 || TimeoutInSeconds > MaximumTimeoutInSeconds)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(TimeoutInSeconds),
                    TimeoutInSeconds,
                    $"ExecuteService.{optionName}.timeoutInSeconds must be between 1 and {MaximumTimeoutInSeconds} seconds.");
            }
        }

        public Command? CreateCommand(TemplateVariables templateVariables)
        {
            if (string.IsNullOrWhiteSpace(ProcessName))
            {
                return null;
            }

            var command = Cli.Wrap(templateVariables.Expand(ProcessName));
            if (!string.IsNullOrWhiteSpace(ProcessArguments))
            {
                command = command.WithArguments(CommandLineArguments.Parse(templateVariables.Expand(ProcessArguments)));
            }
            if (!string.IsNullOrWhiteSpace(WorkingDirectory))
            {
                command = command.WithWorkingDirectory(templateVariables.Expand(WorkingDirectory));
            }

            return command;
        }
    }
}
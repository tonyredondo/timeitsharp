using System.Collections;
using CliWrap;
using CliWrap.Buffered;
using Spectre.Console;
using TimeIt.Common.Configuration;
using TimeIt.Common.Results;

public static class ProcessCmd
{
    public static async Task<DataPoint> RunAsync(Scenario scenario)
    {
        // Prepare variables
        var cmdString = scenario.ProcessName ?? string.Empty;
        var cmdArguments = scenario.ProcessArguments ?? string.Empty;
        var workingDirectory = scenario.WorkingDirectory ?? string.Empty;
        var cmdTimeout = scenario.Timeout?.MaxDuration ?? 0;
        var timeoutCmdString = scenario.Timeout?.ProcessName ?? string.Empty;
        var timeoutCmdArguments = scenario.Timeout?.ProcessArguments ?? string.Empty;

        var cmdEnvironmentVariables = new Dictionary<string, string?>();
        foreach (DictionaryEntry osEnv in Environment.GetEnvironmentVariables())
        {
            if (osEnv.Key?.ToString() is { Length: > 0 } keyString)
            {
                cmdEnvironmentVariables[keyString] = osEnv.Value?.ToString();
            }
        }

        foreach (var envVar in scenario.EnvironmentVariables)
        {
            cmdEnvironmentVariables[envVar.Key] = envVar.Value;
        }

        // Setup the command
        var cmd = Cli.Wrap(cmdString)
            .WithEnvironmentVariables(cmdEnvironmentVariables)
            .WithWorkingDirectory(workingDirectory);
        if (!string.IsNullOrEmpty(cmdArguments))
        {
            cmd = cmd.WithArguments(cmdArguments);
        }

        // Execute the command
        var dataPoint = new DataPoint
        {
            ShouldContinue = true
        };
        if (cmdTimeout <= 0)
        {
            var cmdResult = await cmd.ExecuteBufferedAsync().ConfigureAwait(false);
            dataPoint.End = DateTime.UtcNow;
            dataPoint.Duration = cmdResult.RunTime;
            dataPoint.Start = dataPoint.End - dataPoint.Duration;
            if (cmdResult.ExitCode != 0)
            {
                dataPoint.Error = cmdResult.StandardError + Environment.NewLine + cmdResult.StandardOutput;
            }
        }
        else
        {
            CancellationTokenSource? timeoutCts = null;
            var cmdCts = new CancellationTokenSource();
            dataPoint.Start = DateTime.UtcNow;
            var cmdTask = cmd.ExecuteBufferedAsync(cmdCts.Token);

            if (!string.IsNullOrEmpty(timeoutCmdString))
            {
                timeoutCts = new CancellationTokenSource();
                _ = RunTimeoutAsync(TimeSpan.FromSeconds(cmdTimeout), timeoutCmdString, timeoutCmdArguments,
                    workingDirectory, cmdTask.ProcessId, () => cmdCts.Cancel(), timeoutCts.Token);
            }
            else
            {
                cmdCts.CancelAfter(TimeSpan.FromSeconds(cmdTimeout));
            }

            try
            {
                var cmdResult = await cmdTask.ConfigureAwait(false);
                dataPoint.End = DateTime.UtcNow;
                timeoutCts?.Cancel();
                dataPoint.Duration = cmdResult.RunTime;
                dataPoint.Start = dataPoint.End - dataPoint.Duration;
                if (cmdResult.ExitCode != 0)
                {
                    dataPoint.Error = cmdResult.StandardError + Environment.NewLine + cmdResult.StandardOutput;
                }
            }
            catch (TaskCanceledException)
            {
                dataPoint.End = DateTime.UtcNow;
                dataPoint.Duration = dataPoint.End - dataPoint.Start;
                dataPoint.Error = "Process timeout.";
            }
            catch (OperationCanceledException)
            {
                dataPoint.End = DateTime.UtcNow;
                dataPoint.Duration = dataPoint.End - dataPoint.Start;
                dataPoint.Error = "Process timeout.";
            }
        }

        return dataPoint;
    }

    static async Task RunTimeoutAsync(TimeSpan timeout, string timeoutCmd, string timeoutArgument,
        string workingDirectory, int targetPid, Action targetCancellation, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(timeout, cancellationToken).ConfigureAwait(false);
            if (!cancellationToken.IsCancellationRequested)
            {
                timeoutCmd = timeoutCmd.Replace("%pid%", targetPid.ToString());
                timeoutArgument = timeoutArgument.Replace("%pid%", targetPid.ToString());
                var cmd = Cli.Wrap(timeoutCmd)
                    .WithWorkingDirectory(workingDirectory);
                if (!string.IsNullOrEmpty(timeoutArgument))
                {
                    cmd = cmd.WithArguments(timeoutArgument);
                }

                var cmdResult = await cmd.ExecuteBufferedAsync().ConfigureAwait(false);
                if (cmdResult.ExitCode != 0)
                {
                    AnsiConsole.MarkupLine($"[red]{cmdResult.StandardError}[/]");
                    AnsiConsole.MarkupLine(cmdResult.StandardOutput);
                }
            }
        }
        catch (TaskCanceledException)
        {
            // Do nothing
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]{ex.Message}[/]");
        }
        finally
        {
            targetCancellation?.Invoke();
        }
    }
}
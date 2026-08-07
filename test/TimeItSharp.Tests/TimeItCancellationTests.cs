using System.Diagnostics;
using CliWrap;
using TimeItSharp.Common.Assertors;
using TimeItSharp.Common.Configuration;
using TimeItSharp.Common.Exporters;
using TimeItSharp.Common.Results;
using TimeItSharp.Common.Services;

namespace TimeItSharp.Tests;

public sealed class TimeItCancellationTests
{
    [Fact]
    public async Task Warmup_cancellation_finishes_started_scenario_and_callbacks_once()
    {
        using var cancellation = new CancellationTokenSource();
        CancellationService.Reset(cancellation);
        var config = new Config
        {
            Count = 2,
            WarmUpCount = 1,
            ProcessName = "echo",
            EnableMetrics = false,
        };
        config.Exporters.Add(new AssemblyLoadInfo { InMemoryType = typeof(ConsoleExporter) });
        config.Services.Add(new AssemblyLoadInfo { InMemoryType = typeof(CancellationService) });
        config.Scenarios.Add(new Scenario { Name = "cancelled" });

        var exitCode = await TimeItEngine.RunAsync(config, cancellationToken: cancellation.Token);

        Assert.Equal(1, exitCode);
        Assert.Equal(1, CancellationService.ScenarioStarts);
        Assert.Equal(1, CancellationService.ScenarioFinishes);
        Assert.Equal(1, CancellationService.AfterAllCalls);
        Assert.Equal(1, CancellationService.FinishCalls);
        Assert.Equal(1, CancellationService.ExecutionStarts);
        Assert.Equal(1, CancellationService.ExecutionEnds);
        Assert.Equal(0, CancellationService.ResultCount);
    }

    [Fact]
    public async Task Pre_cancelled_run_cancels_execute_service_finish_hook()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var executeConfiguration = new ExecuteConfiguration
        {
            OnFinish = OperatingSystem.IsWindows()
                ? new ExecuteConfiguration.ProcessData
                {
                    ProcessName = "cmd.exe",
                    ProcessArguments = "/c ping 127.0.0.1 -n 30 > nul",
                    TimeoutInSeconds = 30,
                }
                : new ExecuteConfiguration.ProcessData
                {
                    ProcessName = "/bin/sh",
                    ProcessArguments = "-c \"sleep 30\"",
                    TimeoutInSeconds = 30,
                },
        };
        var config = new Config
        {
            Count = 1,
            EnableMetrics = false,
            ProcessName = "echo",
        };
        config.Scenarios.Add(new Scenario { Name = "scenario" });
        config.Exporters.Add(new AssemblyLoadInfo { InMemoryType = typeof(ConsoleExporter) });
        config.Services.Add(new AssemblyLoadInfo { InMemoryType = typeof(ExecuteService) });
        var stopwatch = Stopwatch.StartNew();

        var exitCode = await TimeItEngine.RunAsync(
            config,
            new TimeItOptions().AddServiceState<ExecuteService>(executeConfiguration),
            cancellation.Token);

        stopwatch.Stop();
        Assert.Equal(1, exitCode);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Cancellation took {stopwatch.Elapsed}.");
    }


    [Fact]
    public async Task Public_oversized_deadline_is_rejected_before_starting_a_target()
    {
        var marker = Path.Combine(Path.GetTempPath(), $"timeit-invalid-target-{Guid.NewGuid():N}");
        var config = new Config
        {
            Count = 1,
            MaximumDurationInMinutes = 71_583,
            EnableMetrics = false,
            ProcessName = "/bin/sh",
            ProcessArguments = $"-c \"touch '{marker}'\"",
        };
        config.Timeout.MaxDuration = 4_294_968;
        config.Scenarios.Add(new Scenario { Name = "invalid-timeout" });

        try
        {
            await Assert.ThrowsAsync<ArgumentException>(() => TimeItEngine.RunAsync(config));
            Assert.False(File.Exists(marker));
        }
        finally
        {
            File.Delete(marker);
        }
    }

    [Fact]
    public async Task Defensive_oversized_deadline_does_not_orphan_a_started_target()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var marker = Path.Combine(Path.GetTempPath(), $"timeit-target-{Guid.NewGuid():N}");
        var config = new Config
        {
            Count = 1,
            MaximumDurationInMinutes = 100_000,
            EnableMetrics = false,
            ProcessName = "/bin/sh",
            ProcessArguments = $"-c \"sleep 1; touch '{marker}'\"",
        };
        config.Timeout.MaxDuration = int.MaxValue;
        var scenario = new Scenario { Name = "large-timeout" };
        config.Scenarios.Add(scenario);
        var callbacks = new TimeItCallbacks();
        var processor = new ScenarioProcessor(
            config,
            new TemplateVariables(),
            Array.Empty<IAssertor>(),
            Array.Empty<IService>(),
            callbacks.GetTriggers(),
            ScenarioProcessor.CaptureEnvironmentVariables());
        processor.PrepareScenario(scenario);

        try
        {
            await processor.ProcessScenarioAsync(0, scenario, CancellationToken.None);

            Assert.True(File.Exists(marker), "The processor returned before the started target completed.");
        }
        finally
        {
            File.Delete(marker);
        }
    }

    private sealed class CancellationService : IService
    {
        private static CancellationTokenSource? _cancellation;

        public static int ScenarioStarts;
        public static int ScenarioFinishes;
        public static int AfterAllCalls;
        public static int FinishCalls;
        public static int ExecutionStarts;
        public static int ExecutionEnds;
        public static int ResultCount;

        public string Name => nameof(CancellationService);

        public static void Reset(CancellationTokenSource cancellation)
        {
            _cancellation = cancellation;
            ScenarioStarts = 0;
            ScenarioFinishes = 0;
            AfterAllCalls = 0;
            FinishCalls = 0;
            ExecutionStarts = 0;
            ExecutionEnds = 0;
            ResultCount = -1;
        }

        public void Initialize(InitOptions options, TimeItCallbacks callbacks)
        {
            callbacks.OnScenarioStart += _ => ScenarioStarts++;
            callbacks.OnExecutionStart += OnExecutionStart;
            callbacks.OnExecutionEnd += (_, _) => ExecutionEnds++;
            callbacks.OnScenarioFinish += result =>
            {
                ScenarioFinishes++;
                ResultCount = result.Count;
                Assert.Equal(Status.Failed, result.Status);
            };
            callbacks.AfterAllScenariosFinishes += _ => AfterAllCalls++;
            callbacks.OnFinish += () => FinishCalls++;
        }

        private static void OnExecutionStart(DataPoint dataPoint, TimeItPhase phase, ref Command command)
        {
            ExecutionStarts++;
            if (phase == TimeItPhase.WarmUp)
            {
                _cancellation!.Cancel();
            }
        }

        public object? GetExecutionServiceData() => null;
        public object? GetScenarioServiceData() => null;
    }
}

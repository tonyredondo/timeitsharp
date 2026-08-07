using CliWrap;
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

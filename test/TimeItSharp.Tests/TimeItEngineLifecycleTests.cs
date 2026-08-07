using TimeItSharp.Common.Assertors;
using TimeItSharp.Common.Configuration;
using TimeItSharp.Common.Exporters;
using TimeItSharp.Common.Results;
using TimeItSharp.Common.Services;

namespace TimeItSharp.Tests;

public sealed class TimeItEngineLifecycleTests
{
    [Fact]
    public async Task Finish_callback_runs_when_service_initialization_fails()
    {
        FailingService.FinishCalls = 0;
        var config = new Config
        {
            Count = 1,
            EnableMetrics = false,
            ProcessName = "echo",
        };
        config.Scenarios.Add(new Scenario { Name = "scenario" });
        config.Services.Add(new AssemblyLoadInfo { InMemoryType = typeof(FailingService) });

        await Assert.ThrowsAsync<InvalidOperationException>(() => TimeItEngine.RunAsync(config));

        Assert.Equal(1, FailingService.FinishCalls);
    }


    [Fact]
    public async Task Assertor_is_initialized_before_enabled_filter_is_applied()
    {
        StateEnabledAssertor.Initialized = false;
        var config = new Config
        {
            Count = 1,
            EnableMetrics = false,
            ProcessName = "echo",
        };
        config.Scenarios.Add(new Scenario { Name = "scenario" });
        config.Assertors.Add(new AssemblyLoadInfo { InMemoryType = typeof(StateEnabledAssertor) });
        config.Exporters.Add(new AssemblyLoadInfo { InMemoryType = typeof(ConsoleExporter) });

        var exitCode = await TimeItEngine.RunAsync(
            config,
            new TimeItOptions().AddAssertorState<StateEnabledAssertor>(true));

        Assert.Equal(0, exitCode);
        Assert.True(StateEnabledAssertor.Initialized);
    }

    [Fact]
    public async Task Callback_failure_returns_failure_but_still_runs_finish()
    {
        CallbackFailingService.AfterAllCalls = 0;
        CallbackFailingService.FinishCalls = 0;
        var config = new Config
        {
            Count = 1,
            EnableMetrics = false,
            ProcessName = "echo",
        };
        config.Scenarios.Add(new Scenario { Name = "scenario" });
        config.Services.Add(new AssemblyLoadInfo { InMemoryType = typeof(CallbackFailingService) });

        var exitCode = await TimeItEngine.RunAsync(config);

        Assert.Equal(1, exitCode);
        Assert.Equal(1, CallbackFailingService.AfterAllCalls);
        Assert.Equal(1, CallbackFailingService.FinishCalls);
    }

    [Fact]
    public async Task Scenario_finish_runs_when_scenario_start_fails()
    {
        ScenarioStartFailingService.StartCalls = 0;
        ScenarioStartFailingService.FinishCalls = 0;
        var config = new Config
        {
            Count = 1,
            EnableMetrics = false,
            ProcessName = "echo",
        };
        config.Scenarios.Add(new Scenario { Name = "scenario" });
        config.Services.Add(new AssemblyLoadInfo { InMemoryType = typeof(ScenarioStartFailingService) });

        var exitCode = await TimeItEngine.RunAsync(config);

        Assert.Equal(1, exitCode);
        Assert.Equal(1, ScenarioStartFailingService.StartCalls);
        Assert.Equal(1, ScenarioStartFailingService.FinishCalls);
    }

    [Fact]
    public async Task AfterAll_runs_when_before_all_fails()
    {
        BeforeAllFailingService.BeforeAllCalls = 0;
        BeforeAllFailingService.AfterAllCalls = 0;
        BeforeAllFailingService.FinishCalls = 0;
        var config = new Config
        {
            Count = 1,
            EnableMetrics = false,
            ProcessName = "echo",
        };
        config.Scenarios.Add(new Scenario { Name = "scenario" });
        config.Services.Add(new AssemblyLoadInfo { InMemoryType = typeof(BeforeAllFailingService) });

        var exitCode = await TimeItEngine.RunAsync(config);

        Assert.Equal(1, exitCode);
        Assert.Equal(1, BeforeAllFailingService.BeforeAllCalls);
        Assert.Equal(1, BeforeAllFailingService.AfterAllCalls);
        Assert.Equal(1, BeforeAllFailingService.FinishCalls);
    }

    [Fact]
    public void Callback_failures_do_not_skip_later_callbacks()
    {
        var callbacks = new TimeItCallbacks();
        var triggers = callbacks.GetTriggers();
        var calls = 0;
        callbacks.OnFinish += () => throw new InvalidOperationException("first callback failed");
        callbacks.OnFinish += () => calls++;

        Assert.Throws<InvalidOperationException>(() => triggers.Finish());
        Assert.Equal(1, calls);
    }

    [Fact]
    public void Validation_rejects_null_scenario_collection_before_clone()
    {
        var config = new Config
        {
            Count = 1,
            ProcessName = "echo",
            Scenarios = null!,
        };

        Assert.Throws<ArgumentException>(() => config.Validate());
    }

    public sealed class ScenarioStartFailingService : IService
    {
        public static int StartCalls;
        public static int FinishCalls;

        public string Name => nameof(ScenarioStartFailingService);

        public void Initialize(InitOptions options, TimeItCallbacks callbacks)
        {
            callbacks.OnScenarioStart += OnScenarioStart;
            callbacks.OnScenarioFinish += OnScenarioFinish;
        }

        public object? GetExecutionServiceData() => null;

        public object? GetScenarioServiceData() => null;

        private static void OnScenarioStart(TimeItCallbacks.ScenarioStartArg scenario)
        {
            StartCalls++;
            throw new InvalidOperationException("Scenario start failed for test.");
        }

        private static void OnScenarioFinish(ScenarioResult result) => FinishCalls++;
    }

    public sealed class BeforeAllFailingService : IService
    {
        public static int BeforeAllCalls;
        public static int AfterAllCalls;
        public static int FinishCalls;

        public string Name => nameof(BeforeAllFailingService);

        public void Initialize(InitOptions options, TimeItCallbacks callbacks)
        {
            callbacks.BeforeAllScenariosStarts += OnBeforeAll;
            callbacks.AfterAllScenariosFinishes += OnAfterAll;
            callbacks.OnFinish += OnFinish;
        }

        public object? GetExecutionServiceData() => null;

        public object? GetScenarioServiceData() => null;

        private static void OnBeforeAll(IReadOnlyList<Scenario> scenarios)
        {
            BeforeAllCalls++;
            throw new InvalidOperationException("Before-all failed for test.");
        }

        private static void OnAfterAll(IReadOnlyList<ScenarioResult> results) => AfterAllCalls++;

        private static void OnFinish() => FinishCalls++;
    }

    public sealed class CallbackFailingService : IService
    {
        public static int AfterAllCalls;
        public static int FinishCalls;

        public string Name => nameof(CallbackFailingService);

        public void Initialize(InitOptions options, TimeItCallbacks callbacks)
        {
            callbacks.AfterAllScenariosFinishes += OnAfterAll;
            callbacks.OnFinish += OnFinish;
        }

        public object? GetExecutionServiceData() => null;

        public object? GetScenarioServiceData() => null;

        private static void OnAfterAll(IReadOnlyList<ScenarioResult> results)
        {
            AfterAllCalls++;
            throw new InvalidOperationException("After-all failed for test.");
        }

        private static void OnFinish() => FinishCalls++;
    }

    public sealed class StateEnabledAssertor : IAssertor
    {
        public static bool Initialized;
        private bool _enabled;

        public string Name => nameof(StateEnabledAssertor);
        public bool Enabled => _enabled;

        public void Initialize(InitOptions options)
        {
            Initialized = true;
            _enabled = options.State is true;
        }

        public AssertResponse ScenarioAssertion(ScenarioResult scenarioResult) => new(Status.Passed);
        public AssertResponse ExecutionAssertion(in AssertionData data) => new(Status.Passed);
    }

    public sealed class FailingService : IService
    {
        public static int FinishCalls;

        public string Name => nameof(FailingService);

        public void Initialize(InitOptions options, TimeItCallbacks callbacks)
        {
            callbacks.OnFinish += OnFinish;
            throw new InvalidOperationException("Initialization failed for test.");
        }

        public object? GetExecutionServiceData() => null;

        public object? GetScenarioServiceData() => null;

        private static void OnFinish() => FinishCalls++;
    }
}

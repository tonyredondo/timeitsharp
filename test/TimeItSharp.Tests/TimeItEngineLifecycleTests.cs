using CliWrap;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
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

    [Fact]
    public async Task Global_duration_budget_is_not_compared_to_elapsed_time_twice()
    {
        var config = new Config
        {
            Count = 100,
            MaximumDurationInMinutes = 1,
            AcceptableRelativeWidth = double.Epsilon,
            MinimumErrorReduction = 0,
            EnableMetrics = false,
            ProcessName = "echo",
        };
        var scenario = new Scenario { Name = "duration-budget" };
        config.Scenarios.Add(scenario);
        var callbacks = new TimeItCallbacks();
        callbacks.OnExecutionStart += DelayExecutionStart;
        var processor = new ScenarioProcessor(
            config,
            new TemplateVariables(),
            Array.Empty<IAssertor>(),
            Array.Empty<IService>(),
            callbacks.GetTriggers(),
            ScenarioProcessor.CaptureEnvironmentVariables());
        processor.PrepareScenario(scenario);
        typeof(ScenarioProcessor)
            .GetField("_remainingDuration", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(processor, TimeSpan.FromMilliseconds(1_200));
        var stopwatch = Stopwatch.StartNew();

        await processor.ProcessScenarioAsync(0, scenario, CancellationToken.None);

        stopwatch.Stop();
        Assert.True(
            stopwatch.Elapsed >= TimeSpan.FromMilliseconds(900),
            $"The 1.2 second global budget stopped after only {stopwatch.Elapsed}.");
    }

    private static void DelayExecutionStart(DataPoint dataPoint, TimeItPhase phase, ref Command command)
        => Thread.Sleep(50);

    [Fact]
    public async Task Pre_cancelled_run_disposes_equal_exporter_instances_by_reference()
    {
        EqualDisposableExporter.Reset();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var config = CreateConfig();
        config.Exporters.Add(new AssemblyLoadInfo { InMemoryType = typeof(EqualDisposableExporter) });
        config.Exporters.Add(new AssemblyLoadInfo { InMemoryType = typeof(EqualDisposableExporter) });

        var exitCode = await TimeItEngine.RunAsync(config, cancellationToken: cancellation.Token);

        Assert.Equal(1, exitCode);
        Assert.Equal(2, EqualDisposableExporter.Created);
        Assert.Equal(2, EqualDisposableExporter.Disposed);
    }



    [Fact]
    public async Task Resolved_exporter_is_disposed_when_service_initialization_fails_first()
    {
        NeverInitializedDisposableExporter.Reset();
        var config = CreateConfig();
        config.Exporters.Add(new AssemblyLoadInfo { InMemoryType = typeof(NeverInitializedDisposableExporter) });
        config.Services.Add(new AssemblyLoadInfo { InMemoryType = typeof(FailingService) });

        await Assert.ThrowsAsync<InvalidOperationException>(() => TimeItEngine.RunAsync(config));

        Assert.Equal(1, NeverInitializedDisposableExporter.Created);
        Assert.Equal(0, NeverInitializedDisposableExporter.Initialized);
        Assert.Equal(1, NeverInitializedDisposableExporter.Disposed);
    }

    [Fact]
    public async Task Ordinary_dispose_failure_forces_false_outcome_before_aware_dispose()
    {
        OutcomeRecordingExporter.Reset();
        var config = CreateConfig();
        config.Exporters.Add(new AssemblyLoadInfo { InMemoryType = typeof(ThrowingDisposeExporter) });
        config.Exporters.Add(new AssemblyLoadInfo { InMemoryType = typeof(OutcomeRecordingExporter) });

        var exitCode = await TimeItEngine.RunAsync(config);

        Assert.Equal(1, exitCode);
        Assert.Equal(new[] { "outcome:False", "dispose" }, OutcomeRecordingExporter.Events);
    }

    [Fact]
    public async Task Outcome_setter_failure_renotifies_previous_exporters_with_failure()
    {
        OutcomeRecordingExporter.Reset();
        var config = CreateConfig();
        config.Exporters.Add(new AssemblyLoadInfo { InMemoryType = typeof(OutcomeRecordingExporter) });
        config.Exporters.Add(new AssemblyLoadInfo { InMemoryType = typeof(ThrowingOutcomeExporter) });

        var exitCode = await TimeItEngine.RunAsync(config);

        Assert.Equal(1, exitCode);
        Assert.Equal(
            new[] { "outcome:True", "outcome:False", "dispose" },
            OutcomeRecordingExporter.Events);
    }

    [Fact]
    public async Task Exporter_receives_failed_final_outcome_after_finish_and_before_dispose()
    {
        OutcomeRecordingExporter.Reset();
        var config = CreateConfig();
        config.Exporters.Add(new AssemblyLoadInfo { InMemoryType = typeof(OutcomeRecordingExporter) });
        config.Services.Add(new AssemblyLoadInfo { InMemoryType = typeof(FailingFinishService) });

        var exitCode = await TimeItEngine.RunAsync(config);

        Assert.Equal(1, exitCode);
        Assert.Equal(new[] { "outcome:False", "dispose" }, OutcomeRecordingExporter.Events);
    }

    [Fact]
    public async Task Execute_service_finish_hook_is_bounded()
    {
        var executeConfiguration = new ExecuteConfiguration
        {
            OnFinish = CreateLongRunningHook(timeoutInSeconds: 1),
        };
        var config = CreateConfig();
        config.Exporters.Add(new AssemblyLoadInfo { InMemoryType = typeof(ConsoleExporter) });
        config.Services.Add(new AssemblyLoadInfo { InMemoryType = typeof(ExecuteService) });
        var stopwatch = Stopwatch.StartNew();

        var exitCode = await TimeItEngine.RunAsync(
            config,
            new TimeItOptions().AddServiceState<ExecuteService>(executeConfiguration));

        stopwatch.Stop();
        Assert.Equal(0, exitCode);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"Hook took {stopwatch.Elapsed}.");
    }


    [Fact]
    public void Execute_configuration_preserves_existing_json_with_bounded_default()
    {
        using var document = JsonDocument.Parse("{ \"processName\": \"echo\" }");
        var configuration = new ExecuteConfiguration(new Dictionary<string, JsonElement?>
        {
            ["onFinish"] = document.RootElement.Clone(),
        });

        Assert.Equal(300, configuration.OnFinish!.TimeoutInSeconds);
    }

    [Fact]
    public void Execute_service_rejects_invalid_programmatic_timeout()
    {
        var executeConfiguration = new ExecuteConfiguration
        {
            OnFinish = new ExecuteConfiguration.ProcessData
            {
                ProcessName = "echo",
                TimeoutInSeconds = 0,
            },
        };
        var service = new ExecuteService();

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => service.Initialize(
            new InitOptions(new Config(), null, new TemplateVariables(), executeConfiguration),
            new TimeItCallbacks()));

        Assert.Contains("timeoutInSeconds", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Cancellation_deadlines_are_capped_before_scheduling()
    {
        var deadline = ScenarioProcessor.GetCancellationDelay(int.MaxValue);

        Assert.True(deadline > TimeSpan.Zero);
        Assert.True(deadline + TimeSpan.FromMilliseconds(250) <=
                    TimeSpan.FromMilliseconds(uint.MaxValue - 1d));
        Assert.Throws<ArgumentOutOfRangeException>(() => ScenarioProcessor.GetCancellationDelay(double.NaN));
    }

    private static Config CreateConfig()
    {
        var config = new Config
        {
            Count = 1,
            EnableMetrics = false,
            ProcessName = "echo",
        };
        config.Scenarios.Add(new Scenario { Name = "scenario" });
        return config;
    }

    private static ExecuteConfiguration.ProcessData CreateLongRunningHook(int timeoutInSeconds)
    {
        return OperatingSystem.IsWindows()
            ? new ExecuteConfiguration.ProcessData
            {
                ProcessName = "cmd.exe",
                ProcessArguments = "/c ping 127.0.0.1 -n 30 > nul",
                TimeoutInSeconds = timeoutInSeconds,
            }
            : new ExecuteConfiguration.ProcessData
            {
                ProcessName = "/bin/sh",
                ProcessArguments = "-c \"sleep 30\"",
                TimeoutInSeconds = timeoutInSeconds,
            };
    }

    public sealed class EqualDisposableExporter : IExporter, IDisposable
    {
        private static int _created;
        private static int _disposed;

        public EqualDisposableExporter() => Interlocked.Increment(ref _created);

        public static int Created => Volatile.Read(ref _created);
        public static int Disposed => Volatile.Read(ref _disposed);
        public string Name => nameof(EqualDisposableExporter);
        public bool Enabled => false;

        public static void Reset()
        {
            Volatile.Write(ref _created, 0);
            Volatile.Write(ref _disposed, 0);
        }

        public void Initialize(InitOptions options)
        {
        }

        public void Export(TimeitResult results)
        {
        }

        public void Dispose() => Interlocked.Increment(ref _disposed);

        public override bool Equals(object? obj) => obj is EqualDisposableExporter;
        public override int GetHashCode() => 1;
    }

    public sealed class FailingFinishService : IService
    {
        public string Name => nameof(FailingFinishService);

        public void Initialize(InitOptions options, TimeItCallbacks callbacks)
        {
            callbacks.OnFinish += () => throw new InvalidOperationException("finish failed");
        }

        public object? GetExecutionServiceData() => null;
        public object? GetScenarioServiceData() => null;
    }

    public sealed class OutcomeRecordingExporter : IExporter, IRunOutcomeAwareExporter, IDisposable
    {
        private static readonly List<string> RecordedEvents = new();

        public static IReadOnlyList<string> Events
        {
            get
            {
                lock (RecordedEvents)
                {
                    return RecordedEvents.ToArray();
                }
            }
        }

        public string Name => nameof(OutcomeRecordingExporter);
        public bool Enabled => false;

        public static void Reset()
        {
            lock (RecordedEvents)
            {
                RecordedEvents.Clear();
            }
        }

        public void Initialize(InitOptions options)
        {
        }

        public void Export(TimeitResult results)
        {
        }

        public void SetRunOutcome(bool succeeded)
        {
            lock (RecordedEvents)
            {
                RecordedEvents.Add($"outcome:{succeeded}");
            }
        }

        public void Dispose()
        {
            lock (RecordedEvents)
            {
                RecordedEvents.Add("dispose");
            }
        }
    }

    public sealed class NeverInitializedDisposableExporter : IExporter, IDisposable
    {
        private static int _created;
        private static int _initialized;
        private static int _disposed;

        public NeverInitializedDisposableExporter() => Interlocked.Increment(ref _created);

        public static int Created => Volatile.Read(ref _created);
        public static int Initialized => Volatile.Read(ref _initialized);
        public static int Disposed => Volatile.Read(ref _disposed);
        public string Name => nameof(NeverInitializedDisposableExporter);
        public bool Enabled => false;

        public static void Reset()
        {
            Volatile.Write(ref _created, 0);
            Volatile.Write(ref _initialized, 0);
            Volatile.Write(ref _disposed, 0);
        }

        public void Initialize(InitOptions options) => Interlocked.Increment(ref _initialized);
        public void Export(TimeitResult results)
        {
        }
        public void Dispose() => Interlocked.Increment(ref _disposed);
    }

    public sealed class ThrowingDisposeExporter : IExporter, IDisposable
    {
        public string Name => nameof(ThrowingDisposeExporter);
        public bool Enabled => false;
        public void Initialize(InitOptions options)
        {
        }
        public void Export(TimeitResult results)
        {
        }
        public void Dispose() => throw new InvalidOperationException("dispose failed");
    }

    public sealed class ThrowingOutcomeExporter : IExporter, IRunOutcomeAwareExporter, IDisposable
    {
        public string Name => nameof(ThrowingOutcomeExporter);
        public bool Enabled => false;
        public void Initialize(InitOptions options)
        {
        }
        public void Export(TimeitResult results)
        {
        }
        public void SetRunOutcome(bool succeeded) => throw new InvalidOperationException("outcome failed");
        public void Dispose()
        {
        }
    }

}

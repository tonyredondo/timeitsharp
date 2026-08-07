using System.Runtime.ExceptionServices;
using System.Threading;
using CliWrap;
using TimeItSharp.Common.Configuration;
using TimeItSharp.Common.Results;

namespace TimeItSharp.Common.Services;

public sealed class TimeItCallbacks
{
    public delegate void BeforeAllScenariosStartsDelegate(IReadOnlyList<Scenario> scenarios);

    public delegate void OnScenarioStartDelegate(ScenarioStartArg scenario);

    public delegate void OnExecutionStartDelegate(DataPoint dataPoint, TimeItPhase phase, ref Command command);

    public delegate void OnExecutionEndDelegate(DataPoint dataPoint, TimeItPhase phase);

    public delegate void OnScenarioFinishDelegate(ScenarioResult scenarioResults);

    public delegate void AfterAllScenariosFinishesDelegate(IReadOnlyList<ScenarioResult> scenariosResults);

    public delegate void OnFinishDelegate();

    private BeforeAllScenariosStartsDelegate? _beforeAllScenariosStarts;
    private OnScenarioStartDelegate? _onScenarioStart;
    private OnExecutionStartDelegate? _onExecutionStart;
    private OnExecutionEndDelegate? _onExecutionEnd;
    private OnScenarioFinishDelegate? _onScenarioFinish;
    private AfterAllScenariosFinishesDelegate? _afterAllScenariosFinishes;
    private OnFinishDelegate? _onFinish;

    // Invocation lists are materialized when a callback is added/removed rather than on every
    // execution. ExecutionStart/ExecutionEnd run once per datapoint and must not allocate merely
    // to inspect a multicast delegate.
    private BeforeAllScenariosStartsDelegate[] _beforeAllScenariosStartsCallbacks = Array.Empty<BeforeAllScenariosStartsDelegate>();
    private OnScenarioStartDelegate[] _onScenarioStartCallbacks = Array.Empty<OnScenarioStartDelegate>();
    private OnExecutionStartDelegate[] _onExecutionStartCallbacks = Array.Empty<OnExecutionStartDelegate>();
    private OnExecutionEndDelegate[] _onExecutionEndCallbacks = Array.Empty<OnExecutionEndDelegate>();
    private OnScenarioFinishDelegate[] _onScenarioFinishCallbacks = Array.Empty<OnScenarioFinishDelegate>();
    private AfterAllScenariosFinishesDelegate[] _afterAllScenariosFinishesCallbacks = Array.Empty<AfterAllScenariosFinishesDelegate>();
    private OnFinishDelegate[] _onFinishCallbacks = Array.Empty<OnFinishDelegate>();

    public event BeforeAllScenariosStartsDelegate? BeforeAllScenariosStarts
    {
        add { _beforeAllScenariosStarts += value; Refresh(ref _beforeAllScenariosStartsCallbacks, _beforeAllScenariosStarts); }
        remove { _beforeAllScenariosStarts -= value; Refresh(ref _beforeAllScenariosStartsCallbacks, _beforeAllScenariosStarts); }
    }

    public event OnScenarioStartDelegate? OnScenarioStart
    {
        add { _onScenarioStart += value; Refresh(ref _onScenarioStartCallbacks, _onScenarioStart); }
        remove { _onScenarioStart -= value; Refresh(ref _onScenarioStartCallbacks, _onScenarioStart); }
    }

    public event OnExecutionStartDelegate? OnExecutionStart
    {
        add { _onExecutionStart += value; Refresh(ref _onExecutionStartCallbacks, _onExecutionStart); }
        remove { _onExecutionStart -= value; Refresh(ref _onExecutionStartCallbacks, _onExecutionStart); }
    }

    public event OnExecutionEndDelegate? OnExecutionEnd
    {
        add { _onExecutionEnd += value; Refresh(ref _onExecutionEndCallbacks, _onExecutionEnd); }
        remove { _onExecutionEnd -= value; Refresh(ref _onExecutionEndCallbacks, _onExecutionEnd); }
    }

    public event OnScenarioFinishDelegate? OnScenarioFinish
    {
        add { _onScenarioFinish += value; Refresh(ref _onScenarioFinishCallbacks, _onScenarioFinish); }
        remove { _onScenarioFinish -= value; Refresh(ref _onScenarioFinishCallbacks, _onScenarioFinish); }
    }

    public event AfterAllScenariosFinishesDelegate? AfterAllScenariosFinishes
    {
        add { _afterAllScenariosFinishes += value; Refresh(ref _afterAllScenariosFinishesCallbacks, _afterAllScenariosFinishes); }
        remove { _afterAllScenariosFinishes -= value; Refresh(ref _afterAllScenariosFinishesCallbacks, _afterAllScenariosFinishes); }
    }

    public event OnFinishDelegate? OnFinish
    {
        add { _onFinish += value; Refresh(ref _onFinishCallbacks, _onFinish); }
        remove { _onFinish -= value; Refresh(ref _onFinishCallbacks, _onFinish); }
    }

    public CallbacksTriggers GetTriggers() => new(this);

    private static void Refresh<TDelegate>(ref TDelegate[] target, TDelegate? callbacks)
        where TDelegate : Delegate
    {
        var snapshot = callbacks is null
            ? Array.Empty<TDelegate>()
            : callbacks.GetInvocationList().Cast<TDelegate>().ToArray();
        Volatile.Write(ref target, snapshot);
    }

    public sealed class CallbacksTriggers
    {
        private readonly TimeItCallbacks _callbacks;

        internal CallbacksTriggers(TimeItCallbacks callbacks)
        {
            _callbacks = callbacks;
        }

        public void BeforeAllScenariosStarts(IReadOnlyList<Scenario> scenarios)
        {
            var callbacks = Volatile.Read(ref _callbacks._beforeAllScenariosStartsCallbacks);
            if (callbacks.Length == 0)
            {
                return;
            }

            Exception? firstException = null;
            foreach (var callback in callbacks)
            {
                try
                {
                    callback(scenarios);
                }
                catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException)
                {
                    firstException ??= ex;
                }
            }

            Rethrow(firstException);
        }

        public void ScenarioStart(ScenarioStartArg scenarioStartArg)
        {
            var callbacks = Volatile.Read(ref _callbacks._onScenarioStartCallbacks);
            if (callbacks.Length == 0)
            {
                return;
            }

            Exception? firstException = null;
            foreach (var callback in callbacks)
            {
                try
                {
                    callback(scenarioStartArg);
                }
                catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException)
                {
                    firstException ??= ex;
                }
            }

            Rethrow(firstException);
        }

        public void ExecutionStart(DataPoint dataPoint, TimeItPhase phase, ref Command command)
        {
            var callbacks = Volatile.Read(ref _callbacks._onExecutionStartCallbacks);
            if (callbacks.Length == 0)
            {
                return;
            }

            InvokeExecutionStartCallbacks(callbacks, dataPoint, phase, ref command, dataPoint.Scenario?.ParentService);
        }

        public void ExecutionEnd(DataPoint dataPoint, TimeItPhase phase)
        {
            var callbacks = Volatile.Read(ref _callbacks._onExecutionEndCallbacks);
            if (callbacks.Length == 0)
            {
                return;
            }

            var parentService = dataPoint.Scenario?.ParentService;
            Exception? firstException = null;
            if (parentService is null)
            {
                // The single-callback path is common and avoids both GetInvocationList and a
                // closure/delegate allocation. The array itself is refreshed only at registration.
                if (callbacks.Length == 1)
                {
                    try
                    {
                        callbacks[0](dataPoint, phase);
                    }
                    catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException)
                    {
                        firstException = ex;
                    }

                    Rethrow(firstException);
                    return;
                }

                foreach (var callback in callbacks)
                {
                    try
                    {
                        callback(dataPoint, phase);
                    }
                    catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException)
                    {
                        firstException ??= ex;
                    }
                }
            }
            else
            {
                foreach (var callback in callbacks)
                {
                    if (callback.Target != parentService)
                    {
                        continue;
                    }

                    try
                    {
                        callback(dataPoint, phase);
                    }
                    catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException)
                    {
                        firstException ??= ex;
                    }
                }
            }

            Rethrow(firstException);
        }

        public void ScenarioFinish(ScenarioResult scenarioResults)
        {
            var callbacks = Volatile.Read(ref _callbacks._onScenarioFinishCallbacks);
            if (callbacks.Length == 0)
            {
                return;
            }

            Exception? firstException = null;
            foreach (var callback in callbacks)
            {
                try
                {
                    callback(scenarioResults);
                }
                catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException)
                {
                    firstException ??= ex;
                }
            }

            Rethrow(firstException);
        }

        public void AfterAllScenariosFinishes(IReadOnlyList<ScenarioResult> scenariosResults)
        {
            var callbacks = Volatile.Read(ref _callbacks._afterAllScenariosFinishesCallbacks);
            if (callbacks.Length == 0)
            {
                return;
            }

            Exception? firstException = null;
            foreach (var callback in callbacks)
            {
                try
                {
                    callback(scenariosResults);
                }
                catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException)
                {
                    firstException ??= ex;
                }
            }

            Rethrow(firstException);
        }

        public void Finish()
        {
            var callbacks = Volatile.Read(ref _callbacks._onFinishCallbacks);
            if (callbacks.Length == 0)
            {
                return;
            }

            Exception? firstException = null;
            foreach (var callback in callbacks)
            {
                try
                {
                    callback();
                }
                catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException)
                {
                    firstException ??= ex;
                }
            }

            Rethrow(firstException);
        }

        private static void InvokeExecutionStartCallbacks(
            OnExecutionStartDelegate[] callbacks,
            DataPoint dataPoint,
            TimeItPhase phase,
            ref Command command,
            IService? parentService)
        {
            Exception? firstException = null;
            if (parentService is null)
            {
                if (callbacks.Length == 1)
                {
                    try
                    {
                        callbacks[0](dataPoint, phase, ref command);
                    }
                    catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException)
                    {
                        firstException = ex;
                    }

                    Rethrow(firstException);
                    return;
                }

                foreach (var callback in callbacks)
                {
                    try
                    {
                        callback(dataPoint, phase, ref command);
                    }
                    catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException)
                    {
                        firstException ??= ex;
                    }
                }
            }
            else
            {
                foreach (var callback in callbacks)
                {
                    if (callback.Target != parentService)
                    {
                        continue;
                    }

                    try
                    {
                        callback(dataPoint, phase, ref command);
                    }
                    catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException)
                    {
                        firstException ??= ex;
                    }
                }
            }

            Rethrow(firstException);
        }

        private static void Rethrow(Exception? exception)
        {
            if (exception is not null)
            {
                ExceptionDispatchInfo.Capture(exception).Throw();
            }
        }
    }

    public sealed class ScenarioStartArg
    {
        private readonly List<(IService ServiceAskingForRepeat, int Count)> _repeats;
        private int _totalRepeatCount;

        public Scenario Scenario { get; private set; }

        internal ScenarioStartArg(Scenario scenario)
        {
            _repeats = new();
            Scenario = scenario;
        }

        public void RepeatScenarioForService(IService serviceAskingForRepeat, int count)
        {
            ArgumentNullException.ThrowIfNull(serviceAskingForRepeat);
            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "A repeated scenario count must be greater than zero.");
            }

            if (count > Config.MaxIterations - _totalRepeatCount)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count,
                    $"Repeated scenario runs cannot exceed {Config.MaxIterations} in total.");
            }

            _totalRepeatCount += count;
            _repeats.Add((serviceAskingForRepeat, count));
        }

        internal IReadOnlyList<(IService ServiceAskingForRepeat, int Count)> GetRepeats()
            => _repeats;
    }
}

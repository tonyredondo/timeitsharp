using System.Runtime.ExceptionServices;
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

    public event BeforeAllScenariosStartsDelegate? BeforeAllScenariosStarts;
    public event OnScenarioStartDelegate? OnScenarioStart;
    public event OnExecutionStartDelegate? OnExecutionStart;
    public event OnExecutionEndDelegate? OnExecutionEnd;
    public event OnScenarioFinishDelegate? OnScenarioFinish;
    public event AfterAllScenariosFinishesDelegate? AfterAllScenariosFinishes;
    public event OnFinishDelegate? OnFinish;

    public CallbacksTriggers GetTriggers() => new(this);
    
    public sealed class CallbacksTriggers
    {
        private readonly TimeItCallbacks _callbacks;

        internal CallbacksTriggers(TimeItCallbacks callbacks)
        {
            _callbacks = callbacks;
        }

        public void BeforeAllScenariosStarts(IReadOnlyList<Scenario> scenarios)
            => InvokeAll(_callbacks.BeforeAllScenariosStarts, callback => callback(scenarios));

        public void ScenarioStart(ScenarioStartArg scenarioStartArg)
            => InvokeAll(_callbacks.OnScenarioStart, callback => callback(scenarioStartArg));

        public void ExecutionStart(DataPoint dataPoint, TimeItPhase phase, ref Command command)
        {
            InvokeExecutionStartCallbacks(
                _callbacks.OnExecutionStart,
                dataPoint,
                phase,
                ref command,
                dataPoint.Scenario?.ParentService);
        }

        public void ExecutionEnd(DataPoint dataPoint, TimeItPhase phase)
        {
            if (dataPoint.Scenario?.ParentService is { } parentService)
            {
                Exception? firstException = null;
                if (_callbacks.OnExecutionEnd is { } callbacks)
                {
                    foreach (var callback in callbacks.GetInvocationList())
                    {
                        if (callback.Target != parentService)
                        {
                            continue;
                        }

                        try
                        {
                            ((OnExecutionEndDelegate)callback).Invoke(dataPoint, phase);
                        }
                        catch (Exception ex)
                        {
                            firstException ??= ex;
                        }
                    }
                }

                Rethrow(firstException);
            }
            else
            {
                InvokeAll(_callbacks.OnExecutionEnd, callback => callback(dataPoint, phase));
            }
        }

        public void ScenarioFinish(ScenarioResult scenarioResults)
            => InvokeAll(_callbacks.OnScenarioFinish, callback => callback(scenarioResults));

        public void AfterAllScenariosFinishes(IReadOnlyList<ScenarioResult> scenariosResults)
            => InvokeAll(_callbacks.AfterAllScenariosFinishes, callback => callback(scenariosResults));

        public void Finish()
            => InvokeAll(_callbacks.OnFinish, callback => callback());

        private static void InvokeExecutionStartCallbacks(
            OnExecutionStartDelegate? callbacks,
            DataPoint dataPoint,
            TimeItPhase phase,
            ref Command command,
            IService? parentService)
        {
            Exception? firstException = null;
            if (callbacks is not null)
            {
                foreach (var callback in callbacks.GetInvocationList())
                {
                    if (parentService is not null && callback.Target != parentService)
                    {
                        continue;
                    }

                    try
                    {
                        ((OnExecutionStartDelegate)callback).Invoke(dataPoint, phase, ref command);
                    }
                    catch (Exception ex)
                    {
                        firstException ??= ex;
                    }
                }
            }

            Rethrow(firstException);
        }

        private static void InvokeAll<TDelegate>(TDelegate? callbacks, Action<TDelegate> invoke)
            where TDelegate : Delegate
        {
            if (callbacks is null)
            {
                return;
            }

            Exception? firstException = null;
            foreach (var callback in callbacks.GetInvocationList())
            {
                try
                {
                    invoke((TDelegate)callback);
                }
                catch (Exception ex)
                {
                    firstException ??= ex;
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

        public Scenario Scenario { get; private set; }

        internal ScenarioStartArg(Scenario scenario)
        {
            _repeats = new();
            Scenario = scenario;
        }
        
        public void RepeatScenarioForService(IService serviceAskingForRepeat, int count)
        {
            _repeats.Add((serviceAskingForRepeat, count));
        }

        internal IEnumerable<(IService ServiceAskingForRepeat, int Count)> GetRepeats()
            => _repeats;
    }
}

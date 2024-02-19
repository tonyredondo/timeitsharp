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
            => _callbacks.BeforeAllScenariosStarts?.Invoke(scenarios);

        public void ScenarioStart(ScenarioStartArg scenarioStartArg)
            => _callbacks.OnScenarioStart?.Invoke(scenarioStartArg);

        public void ExecutionStart(DataPoint dataPoint, TimeItPhase phase, ref Command command)
        {
            if (dataPoint.Scenario?.ParentService is { } parentService)
            {
                if (_callbacks.OnExecutionStart is { } onExecutionStartEvent)
                {
                    foreach (var @delegate in onExecutionStartEvent.GetInvocationList())
                    {
                        if (@delegate.Target == parentService)
                        {
                            ((OnExecutionStartDelegate)@delegate).Invoke(dataPoint, phase, ref command);
                        }
                    }
                }
            }
            else
            {
                _callbacks.OnExecutionStart?.Invoke(dataPoint, phase, ref command);
            }
        }

        public void ExecutionEnd(DataPoint dataPoint, TimeItPhase phase)
        {
            if (dataPoint.Scenario?.ParentService is { } parentService)
            {
                if (_callbacks.OnExecutionEnd is { } onExecutionEndEvent)
                {
                    foreach (var @delegate in onExecutionEndEvent.GetInvocationList())
                    {
                        if (@delegate.Target == parentService)
                        {
                            ((OnExecutionEndDelegate)@delegate).Invoke(dataPoint, phase);
                        }
                    }
                }
            }
            else
            {
                _callbacks.OnExecutionEnd?.Invoke(dataPoint, phase);
            }
        }

        public void ScenarioFinish(ScenarioResult scenarioResults)
            => _callbacks.OnScenarioFinish?.Invoke(scenarioResults);

        public void AfterAllScenariosFinishes(IReadOnlyList<ScenarioResult> scenariosResults)
            => _callbacks.AfterAllScenariosFinishes?.Invoke(scenariosResults);

        public void Finish()
            => _callbacks.OnFinish?.Invoke();
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
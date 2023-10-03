using CliWrap;
using TimeItSharp.Common.Configuration;
using TimeItSharp.Common.Results;

namespace TimeItSharp.Common.Services;

public sealed class TimeItCallbacks
{
    public delegate void BeforeAllScenariosStartsDelegate(IReadOnlyList<Scenario> scenarios);

    public delegate void OnScenarioStartDelegate(Scenario scenario);

    public delegate void OnExecutionStartDelegate(DataPoint dataPoint, ref Command command);

    public delegate void OnExecutionEndDelegate(DataPoint dataPoint);
    
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

        public void ScenarioStart(Scenario scenario)
            => _callbacks.OnScenarioStart?.Invoke(scenario);

        public void ExecutionStart(DataPoint dataPoint, ref Command command)
            => _callbacks.OnExecutionStart?.Invoke(dataPoint, ref command);

        public void ExecutionEnd(DataPoint dataPoint)
            => _callbacks.OnExecutionEnd?.Invoke(dataPoint);

        public void ScenarioFinish(ScenarioResult scenarioResults)
            => _callbacks.OnScenarioFinish?.Invoke(scenarioResults);

        public void AfterAllScenariosFinishes(IReadOnlyList<ScenarioResult> scenariosResults)
            => _callbacks.AfterAllScenariosFinishes?.Invoke(scenariosResults);

        public void Finish()
            => _callbacks.OnFinish?.Invoke();
    }
}
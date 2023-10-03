using TimeItSharp.Common.Assertors;
using TimeItSharp.Common.Exporters;
using TimeItSharp.Common.Services;

namespace TimeItSharp.Common;

public record TimeItOptions(TemplateVariables? TemplateVariables = null)
{
    private Dictionary<Type, object> _statesByType = new();

    internal Dictionary<Type, object> StatesByType => _statesByType;

    public void AddExporterState<TExporter>(object state)
        where TExporter : IExporter
    {
        _statesByType[typeof(TExporter)] = state;
    }

    public void AddAssertorState<TAssertor>(object state)
        where TAssertor : IAssertor
    {
        _statesByType[typeof(TAssertor)] = state;
    }

    public void AddServiceState<TService>(object state)
        where TService : IService
    {
        _statesByType[typeof(TService)] = state;
    }
}
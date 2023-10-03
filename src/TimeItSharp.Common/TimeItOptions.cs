using TimeItSharp.Common.Assertors;
using TimeItSharp.Common.Exporters;
using TimeItSharp.Common.Services;

namespace TimeItSharp.Common;

public record TimeItOptions(TemplateVariables? TemplateVariables = null)
{
    private Dictionary<Type, object> _statesByType = new();

    internal Dictionary<Type, object> StatesByType => _statesByType;

    public TimeItOptions AddExporterState<TExporter>(object state)
        where TExporter : IExporter
    {
        _statesByType[typeof(TExporter)] = state;
        return this;
    }

    public TimeItOptions AddExporterState<TExporter, TExporter2>(object state)
        where TExporter : IExporter
        where TExporter2 : IExporter
    {
        _statesByType[typeof(TExporter)] = state;
        _statesByType[typeof(TExporter2)] = state;
        return this;
    }
    
    public TimeItOptions AddExporterState<TExporter, TExporter2, TExporter3>(object state)
        where TExporter : IExporter
        where TExporter2 : IExporter
        where TExporter3 : IExporter
    {
        _statesByType[typeof(TExporter)] = state;
        _statesByType[typeof(TExporter2)] = state;
        _statesByType[typeof(TExporter3)] = state;
        return this;
    }

    public TimeItOptions AddAssertorState<TAssertor>(object state)
        where TAssertor : IAssertor
    {
        _statesByType[typeof(TAssertor)] = state;
        return this;
    }
    
    public TimeItOptions AddAssertorState<TAssertor, TAssertor2>(object state)
        where TAssertor : IAssertor
        where TAssertor2 : IAssertor
    {
        _statesByType[typeof(TAssertor)] = state;
        _statesByType[typeof(TAssertor2)] = state;
        return this;
    }

    public TimeItOptions AddAssertorState<TAssertor, TAssertor2, TAssertor3>(object state)
        where TAssertor : IAssertor
        where TAssertor2 : IAssertor
        where TAssertor3 : IAssertor
    {
        _statesByType[typeof(TAssertor)] = state;
        _statesByType[typeof(TAssertor2)] = state;
        _statesByType[typeof(TAssertor3)] = state;
        return this;
    }

    public TimeItOptions AddServiceState<TService>(object state)
        where TService : IService
    {
        _statesByType[typeof(TService)] = state;
        return this;
    }
    
    public TimeItOptions AddServiceState<TService, TService2>(object state)
        where TService : IService
        where TService2 : IService
    {
        _statesByType[typeof(TService)] = state;
        _statesByType[typeof(TService2)] = state;
        return this;
    }

    public TimeItOptions AddServiceState<TService, TService2, TService3>(object state)
        where TService : IService
        where TService2 : IService
        where TService3 : IService
    {
        _statesByType[typeof(TService)] = state;
        _statesByType[typeof(TService2)] = state;
        _statesByType[typeof(TService3)] = state;
        return this;
    }
}
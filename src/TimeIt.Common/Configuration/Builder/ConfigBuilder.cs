using TimeIt.Common.Assertors;
using TimeIt.Common.Exporters;
using TimeIt.Common.Services;

namespace TimeIt.Common.Configuration.Builder;

public sealed class ConfigBuilder
{
    private readonly Config _configuration;

    public ConfigBuilder()
    {
        _configuration = new();
    }

    public ConfigBuilder(Config configuration)
    {
        _configuration = configuration;
    }
    
    public Config Build() => _configuration;

    #region Counts

    public ConfigBuilder WithWarmupCount(int count)
    {
        _configuration.WarmUpCount = count;
        return this;
    }

    public ConfigBuilder WithCount(int count)
    {
        _configuration.Count = count;
        return this;
    }
    
    #endregion

    #region Legacy
    
    public ConfigBuilder WithDatadog(bool enabled)
    {
        _configuration.EnableDatadog = enabled;
        if (enabled && _configuration.Exporters.Find(e => e.Type == typeof(DatadogExporter).FullName || e.Name == "Datadog") is null)
        {
            return WithExporter<DatadogExporter>();
        }

        return this;
    }
    
    public ConfigBuilder WithMetrics(bool enabled)
    {
        _configuration.EnableMetrics = enabled;
        return this;
    }
    
    #endregion

    #region Scenarios

    public ConfigBuilder ClearScenarios()
    {
        _configuration.Scenarios.Clear();
        return this;
    }

    public ConfigBuilder WithScenario(ScenarioBuilder scenarioBuilder)
    {
        _configuration.Scenarios.Add(scenarioBuilder.Build());
        return this;
    }
    
    public ConfigBuilder WithScenario(Func<ScenarioBuilder, ScenarioBuilder> scenarioBuilderFunc)
    {
        return WithScenario(scenarioBuilderFunc(new ScenarioBuilder()));
    }

    #endregion
    
    public ConfigBuilder WithJsonExporterPath(string filePath)
    {
        _configuration.JsonExporterFilePath = filePath;
        if (!string.IsNullOrEmpty(filePath) && _configuration.Exporters.Find(e => e.Type == typeof(JsonExporter).FullName || e.Name == nameof(JsonExporter)) is null)
        {
            return WithExporter<JsonExporter>();
        }

        return this;
    }

    #region WithExporter

    public ConfigBuilder ClearExporters()
    {
        _configuration.Exporters.Clear();
        return this;
    }
    
    public ConfigBuilder WithExporter(AssemblyLoadInfo exporter)
    {
        _configuration.Exporters.Add(exporter);
        return this;
    }

    public ConfigBuilder WithExporter(params AssemblyLoadInfo[] exporters)
    {
        _configuration.Exporters.AddRange(exporters);
        return this;
    }
    
    public ConfigBuilder WithExporter(string exporterName)
    {
        _configuration.Exporters.Add(new AssemblyLoadInfo
        {
            Name = exporterName
        });

        return this;
    }

    public ConfigBuilder WithExporter<T>()
        where T : IExporter
    {
        return WithExporter(typeof(T));
    }

    public ConfigBuilder WithExporter(Type exporterType)
    {
        _configuration.Exporters.Add(new AssemblyLoadInfo
        {
            FilePath = exporterType.Assembly.Location,
            Type = exporterType.FullName
        });

        return this;
    }
    
    #endregion

    #region WithAssertor
    
    public ConfigBuilder ClearAssertors()
    {
        _configuration.Assertors.Clear();
        return this;
    }

    public ConfigBuilder WithAssertor(AssemblyLoadInfo assertor)
    {
        _configuration.Assertors.Add(assertor);
        return this;
    }
    
    public ConfigBuilder WithAssertor(params AssemblyLoadInfo[] assertors)
    {
        _configuration.Assertors.AddRange(assertors);
        return this;
    }
    
    public ConfigBuilder WithAssertor(string assertorName)
    {
        _configuration.Assertors.Add(new AssemblyLoadInfo
        {
            Name = assertorName
        });

        return this;
    }

    public ConfigBuilder WithAssertor<T>()
        where T : IAssertor
    {
        return WithAssertor(typeof(T));
    }

    public ConfigBuilder WithAssertor(Type assertorType)
    {
        _configuration.Assertors.Add(new AssemblyLoadInfo
        {
            FilePath = assertorType.Assembly.Location,
            Type = assertorType.FullName
        });

        return this;
    }
    
    #endregion

    #region WithService
    
    public ConfigBuilder ClearServices()
    {
        _configuration.Services.Clear();
        return this;
    }

    public ConfigBuilder WithService(AssemblyLoadInfo service)
    {
        _configuration.Services.Add(service);
        return this;
    }

    public ConfigBuilder WithService(params AssemblyLoadInfo[] services)
    {
        _configuration.Services.AddRange(services);
        return this;
    }
    
    public ConfigBuilder WithService(string serviceName)
    {
        _configuration.Services.Add(new AssemblyLoadInfo
        {
            Name = serviceName
        });

        return this;
    }
    
    public ConfigBuilder WithService<T>()
        where T : IService
    {
        return WithService(typeof(T));
    }

    public ConfigBuilder WithService(Type serviceType)
    {
        _configuration.Services.Add(new AssemblyLoadInfo
        {
            FilePath = serviceType.Assembly.Location,
            Type = serviceType.FullName
        });

        return this;
    }
    
    #endregion
    
    #region ProcessData

    public ConfigBuilder WithProcessName(string? processName)
    {
        _configuration.ProcessName = processName;
        return this;
    }

    public ConfigBuilder WithProcessArguments(string? processArguments)
    {
        _configuration.ProcessArguments = processArguments;
        return this;
    }
    
    public ConfigBuilder WithWorkingDirectory(string? workingDirectory)
    {
        _configuration.WorkingDirectory = workingDirectory;
        return this;
    }

    public ConfigBuilder WithEnvironmentVariables(Dictionary<string, string> environmentVariables)
    {
        foreach (var kv in environmentVariables)
        {
            _configuration.EnvironmentVariables[kv.Key] = kv.Value;
        }

        return this;
    }
    
    public ConfigBuilder WithEnvironmentVariable(string key, string value)
    {
        _configuration.EnvironmentVariables[key] = value;
        return this;
    }

    public ConfigBuilder WithPathValidations(params string[] pathValidations)
    {
        _configuration.PathValidations.AddRange(pathValidations);
        return this;
    }

    public ConfigBuilder WithPathValidations(string pathValidation)
    {
        _configuration.PathValidations.Add(pathValidation);
        return this;
    }

    public ConfigBuilder WithTimeout(TimeoutBuilder timeoutBuilder)
    {
        _configuration.Timeout = timeoutBuilder.Build();
        return this;
    }

    public ConfigBuilder WithTimeout(Func<TimeoutBuilder, TimeoutBuilder> timeoutBuilderFunc)
    {
        return WithTimeout(timeoutBuilderFunc(new TimeoutBuilder(_configuration.Timeout)));
    }
    
    public ConfigBuilder WithTags(Dictionary<string, string> tags)
    {
        foreach (var kv in tags)
        {
            _configuration.Tags[kv.Key] = kv.Value;
        }

        return this;
    }
    
    public ConfigBuilder WithTags(string key, string value)
    {
        _configuration.Tags[key] = value;
        return this;
    }

    #endregion
}
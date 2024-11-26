using System.Diagnostics.CodeAnalysis;
using TimeItSharp.Common.Assertors;
using TimeItSharp.Common.Exporters;
using TimeItSharp.Common.Services;

namespace TimeItSharp.Common.Configuration.Builder;

/// <summary>
/// TimeIt configuration builder
/// </summary>
public sealed class ConfigBuilder
{
    private readonly Config _configuration;

    /// <summary>
    /// Creates a new instance of the TimeIt configuration builder
    /// </summary>
    public ConfigBuilder()
    {
        _configuration = new();
    }

    /// <summary>
    /// Creates a new instance of the TimeIt configuration builder
    /// </summary>
    /// <param name="configuration">Existing configuration instance</param>
    public ConfigBuilder(Config configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Creates a new instance of the TimeIt configuration builder
    /// </summary>
    /// <returns>ConfigBuilder instance</returns>
    public static ConfigBuilder Create() => new();
    
    /// <summary>
    /// Build the configuration from the builder
    /// </summary>
    /// <returns>Config instance</returns>
    public Config Build() => _configuration;

    #region Counts

    /// <summary>
    /// Sets the warmup count
    /// </summary>
    /// <param name="count">Number of times to execute each scenario in warmup phase</param>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithWarmupCount(int count)
    {
        _configuration.WarmUpCount = count;
        return this;
    }

    /// <summary>
    /// Sets the number of iterations of each scenario
    /// </summary>
    /// <param name="count">Number of times to execute each scenario</param>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithCount(int count)
    {
        _configuration.Count = count;
        return this;
    }
    
    #endregion

    #region Legacy
    
    /// <summary>
    /// Sets if the Datadog exporter should be enabled or not
    /// </summary>
    /// <remarks>If true, the Datadog exporter will be added.</remarks>
    /// <param name="enabled">True if the datadog exporter is enabled; false if disabled</param>
    /// <returns>Configuration builder instance</returns>
    [Obsolete("This is a legacy settings, you should just add the exporter to the exporters list")]
    public ConfigBuilder WithDatadog(bool enabled)
    {
        _configuration.EnableDatadog = enabled;
        if (enabled && _configuration.Exporters.Find(e => e.Type == typeof(DatadogExporter).FullName || e.Name == "Datadog") is null)
        {
            return WithExporter<DatadogExporter>();
        }

        return this;
    }
    
    #endregion

    #region Scenarios

    /// <summary>
    /// Clear the scenarios list
    /// </summary>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder ClearScenarios()
    {
        _configuration.Scenarios.Clear();
        return this;
    }

    /// <summary>
    /// Adds a new scenario
    /// </summary>
    /// <param name="scenarioBuilder">ScenarioBuilder instance</param>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithScenario(ScenarioBuilder scenarioBuilder)
    {
        _configuration.Scenarios.Add(scenarioBuilder.Build());
        return this;
    }
    
    /// <summary>
    /// Adds a new scenario
    /// </summary>
    /// <param name="scenarioBuilderFunc">Scenario builder delegate</param>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithScenario(Func<ScenarioBuilder, ScenarioBuilder> scenarioBuilderFunc)
    {
        return WithScenario(scenarioBuilderFunc(new ScenarioBuilder()));
    }

    #endregion
 
    /// <summary>
    /// Sets if the runtime metrics importer should be enabled or not
    /// </summary>
    /// <param name="enabled">True if the metrics importer is enabled; false if disabled</param>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithMetrics(bool enabled)
    {
        _configuration.EnableMetrics = enabled;
        return this;
    }
    
    /// <summary>
    /// Sets the process name to collect runtime metrics
    /// </summary>
    /// <param name="processName">Process name</param>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithMetricsProcessName(string processName)
    {
        _configuration.MetricsProcessName = processName;
        return this;
    }
    
    /// <summary>
    /// Sets the name of the configuration
    /// </summary>
    /// <param name="name">Name of the configuration</param>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithName(string name)
    {
        _configuration.Name = name;
        return this;
    }
    
    /// <summary>
    /// Sets the json exporter path for the JsonExporter
    /// </summary>
    /// <param name="filePath">Filepath to export the json file</param>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithJsonExporterPath(string filePath)
    {
        _configuration.JsonExporterFilePath = filePath;
        if (!string.IsNullOrEmpty(filePath) && _configuration.Exporters.Find(e => e.Type == typeof(JsonExporter).FullName || e.Name == nameof(JsonExporter)) is null)
        {
            return WithExporter<JsonExporter>();
        }

        return this;
    }

    /// <summary>
    /// Sets if the failed data points should be processed
    /// </summary>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder ProcessFailedDataPoints()
    {
        _configuration.ProcessFailedDataPoints = true;
        return this;
    }
    
    /// <summary>
    /// Sets if the standard output should be shown for the first run
    /// </summary>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder ShowStdOutForFirstRun()
    {
        _configuration.ShowStdOutForFirstRun = true;
        return this;
    }

    /// <summary>
    /// Sets timeit to run in debug mode
    /// </summary>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithDebugMode()
    {
        _configuration.DebugMode = true;
        return this;
    }
    
    /// <summary>
    /// Sets the acceptable relative width for the confidence interval where timeit will consider the results as valid and stop iterating
    /// </summary>
    /// <param name="acceptableRelativeWidth">Acceptable relative width</param>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithAcceptableRelativeWidth(double acceptableRelativeWidth)
    {
        _configuration.AcceptableRelativeWidth = acceptableRelativeWidth;
        return this;
    }
    
    /// <summary>
    /// Sets the confidence level for the confidence interval where timeit will compare the acceptable relative width
    /// </summary>
    /// <param name="confidenceLevel">Confidence level</param>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithConfidenceLevel(double confidenceLevel)
    {
        _configuration.ConfidenceLevel = confidenceLevel;
        return this;
    }
    
    /// <summary>
    /// Sets the maximum duration in minutes for all scenarios to run
    /// </summary>
    /// <param name="maximumDurationInMinutes">Maximum number of minutes</param>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithMaximumDurationInMinutes(int maximumDurationInMinutes)
    {
        _configuration.MaximumDurationInMinutes = maximumDurationInMinutes;
        return this;
    }
    
    /// <summary>
    /// Sets the interval in which timeit will evaluate the results and decide if there's error reductions.
    /// </summary>
    /// <param name="evaluationInterval">Interval in number of iterations</param>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithEvaluationInterval(int evaluationInterval)
    {
        _configuration.EvaluationInterval = evaluationInterval;
        return this;
    }
    
    /// <summary>
    /// Sets the minimum error reduction required for timeit to consider the results as valid and stop iterating
    /// </summary>
    /// <param name="minimumErrorReduction">Minimum error reduction required</param>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithMinimumErrorReduction(double minimumErrorReduction)
    {
        _configuration.MinimumErrorReduction = minimumErrorReduction;
        return this;
    }

    #region WithExporter

    /// <summary>
    /// Clears the exporters list
    /// </summary>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder ClearExporters()
    {
        _configuration.Exporters.Clear();
        return this;
    }
    
    /// <summary>
    /// Adds a exporter
    /// </summary>
    /// <param name="exporter">Assembly load info instance of the exporter</param>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithExporter(AssemblyLoadInfo exporter)
    {
        // Check if exporter is already there.
        foreach (var existingExporter in _configuration.Exporters)
        {
            if (existingExporter.Name == exporter.Name &&
                existingExporter.FilePath == exporter.FilePath &&
                existingExporter.Type == exporter.Type)
            {
                return this;
            }
        }

        _configuration.Exporters.Add(exporter);
        return this;
    }

    /// <summary>
    /// Adds multiple exporters
    /// </summary>
    /// <param name="exporters">Assembly load info array</param>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithExporter(params AssemblyLoadInfo[] exporters)
    {
        _configuration.Exporters.AddRange(exporters);
        return this;
    }
    
    /// <summary>
    /// Adds a known exporter by name
    /// </summary>
    /// <param name="exporterName">Exporter name</param>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithExporter(string exporterName)
    {
        // Check if exporter is already there.
        foreach (var exporter in _configuration.Exporters)
        {
            if (exporter.Name == exporterName)
            {
                return this;
            }
        }

        _configuration.Exporters.Add(new AssemblyLoadInfo
        {
            Name = exporterName
        });

        return this;
    }

    /// <summary>
    /// Adds a exporter
    /// </summary>
    /// <typeparam name="T">Type of exporter</typeparam>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithExporter<T>()
        where T : IExporter
    {
        return WithExporter(typeof(T));
    }

    /// <summary>
    /// Adds a exporter
    /// </summary>
    /// <param name="exporterType">Type of exporter</param>
    /// <returns>Configuration builder instance</returns>
    [UnconditionalSuppressMessage("SingleFile", "IL3000:Avoid accessing Assembly file path when publishing as a single file", Justification = "Case is being handled")]
    public ConfigBuilder WithExporter(Type exporterType)
    {
        var exporterTypeLocation = exporterType.Assembly.Location;
        if (string.IsNullOrEmpty(exporterTypeLocation))
        {
            exporterTypeLocation = exporterType.Assembly.GetName().Name + ".dll";
        }

        // Check if exporter is already there.
        foreach (var exporter in _configuration.Exporters)
        {
            if ((exporter.FilePath == exporterTypeLocation || exporter.InMemoryType == exporterType) &&
                exporter.Type == exporterType.FullName)
            {
                return this;
            }
        }
        
        _configuration.Exporters.Add(new AssemblyLoadInfo
        {
            FilePath = exporterTypeLocation,
            Type = exporterType.FullName,
            InMemoryType = exporterType,
        });

        if (exporterType == typeof(DatadogExporter))
        {
            _configuration.EnableDatadog = true;
        }

        return this;
    }
    
    /// <summary>
    /// Adds multiple exporters
    /// </summary>
    /// <typeparam name="T1">Type of exporter</typeparam>
    /// <typeparam name="T2">Type of exporter</typeparam>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithExporters<T1, T2>()
        where T1 : IExporter
        where T2 : IExporter
    {
        return WithExporter(typeof(T1)).WithExporter(typeof(T2));
    }
    
    /// <summary>
    /// Adds multiple exporters
    /// </summary>
    /// <typeparam name="T1">Type of exporter</typeparam>
    /// <typeparam name="T2">Type of exporter</typeparam>
    /// <typeparam name="T3">Type of exporter</typeparam>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithExporters<T1, T2, T3>()
        where T1 : IExporter
        where T2 : IExporter
        where T3 : IExporter
    {
        return WithExporter(typeof(T1)).WithExporter(typeof(T2)).WithExporter(typeof(T3));
    }

    #endregion

    #region WithAssertor
    
    /// <summary>
    /// Clears the assertors list
    /// </summary>
    /// <returns></returns>
    public ConfigBuilder ClearAssertors()
    {
        _configuration.Assertors.Clear();
        return this;
    }

    /// <summary>
    /// Adds an assertor
    /// </summary>
    /// <param name="assertor">Assembly load info instance for the assertor</param>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithAssertor(AssemblyLoadInfo assertor)
    {
        // Check if assertor is already there.
        foreach (var existing in _configuration.Assertors)
        {
            if (existing.Name == assertor.Name &&
                existing.FilePath == assertor.FilePath &&
                existing.Type == assertor.Type)
            {
                return this;
            }
        }

        _configuration.Assertors.Add(assertor);
        return this;
    }
    
    /// <summary>
    /// Adds multiple assertors
    /// </summary>
    /// <param name="assertors">Assembly load info array</param>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithAssertor(params AssemblyLoadInfo[] assertors)
    {
        _configuration.Assertors.AddRange(assertors);
        return this;
    }
    
    /// <summary>
    /// Adds a known assertor by name
    /// </summary>
    /// <param name="assertorName">Assertor name</param>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithAssertor(string assertorName)
    {
        // Check if assertors is already there.
        foreach (var exiting in _configuration.Assertors)
        {
            if (exiting.Name == assertorName)
            {
                return this;
            }
        }

        _configuration.Assertors.Add(new AssemblyLoadInfo
        {
            Name = assertorName
        });

        return this;
    }

    /// <summary>
    /// Add an assertor
    /// </summary>
    /// <typeparam name="T">Type of assertor</typeparam>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithAssertor<T>()
        where T : IAssertor
    {
        return WithAssertor(typeof(T));
    }

    /// <summary>
    /// Add an assertor
    /// </summary>
    /// <param name="assertorType">Type of assertor</param>
    /// <returns>Configuration builder instance</returns>
    [UnconditionalSuppressMessage("SingleFile", "IL3000:Avoid accessing Assembly file path when publishing as a single file", Justification = "Case is being handled")]
    public ConfigBuilder WithAssertor(Type assertorType)
    {
        var assertorTypeLocation = assertorType.Assembly.Location;
        if (string.IsNullOrEmpty(assertorTypeLocation))
        {
            assertorTypeLocation = assertorType.Assembly.GetName().Name + ".dll";
        }

        // Check if assertors is already there.
        foreach (var existing in _configuration.Assertors)
        {
            if ((existing.FilePath == assertorTypeLocation || existing.InMemoryType == assertorType) &&
                existing.Type == assertorType.FullName)
            {
                return this;
            }
        }

        _configuration.Assertors.Add(new AssemblyLoadInfo
        {
            FilePath = assertorTypeLocation,
            Type = assertorType.FullName,
            InMemoryType = assertorType,
        });

        return this;
    }

    /// <summary>
    /// Add multiple assertors
    /// </summary>
    /// <typeparam name="T1">Type of assertor</typeparam>
    /// <typeparam name="T2">Type of assertor</typeparam>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithAssertors<T1, T2>()
        where T1 : IAssertor
        where T2 : IAssertor
    {
        return WithAssertor(typeof(T1)).WithAssertor(typeof(T2));
    }
    
    /// <summary>
    /// Add multiple assertors
    /// </summary>
    /// <typeparam name="T1">Type of assertor</typeparam>
    /// <typeparam name="T2">Type of assertor</typeparam>
    /// <typeparam name="T3">Type of assertor</typeparam>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithAssertors<T1, T2, T3>()
        where T1 : IAssertor
        where T2 : IAssertor
        where T3 : IAssertor
    {
        return WithAssertor(typeof(T1)).WithAssertor(typeof(T2)).WithAssertor(typeof(T3));
    }

    #endregion

    #region WithService
    
    /// <summary>
    /// Clears the services list
    /// </summary>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder ClearServices()
    {
        _configuration.Services.Clear();
        return this;
    }

    /// <summary>
    /// Adds a service
    /// </summary>
    /// <param name="service">Assembly load info instance of the service</param>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithService(AssemblyLoadInfo service)
    {
        // Check if service is already there.
        foreach (var existing in _configuration.Services)
        {
            if (existing.Name == service.Name &&
                existing.FilePath == service.FilePath &&
                existing.Type == service.Type)
            {
                return this;
            }
        }

        _configuration.Services.Add(service);
        return this;
    }

    /// <summary>
    /// Adds a service
    /// </summary>
    /// <param name="services">Assembly load info array</param>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithService(params AssemblyLoadInfo[] services)
    {
        _configuration.Services.AddRange(services);
        return this;
    }
    
    /// <summary>
    /// Adds a known service by name
    /// </summary>
    /// <param name="serviceName">Service name</param>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithService(string serviceName)
    {
        // Check if service is already there.
        foreach (var exiting in _configuration.Services)
        {
            if (exiting.Name == serviceName)
            {
                return this;
            }
        }

        _configuration.Services.Add(new AssemblyLoadInfo
        {
            Name = serviceName
        });

        return this;
    }
    
    /// <summary>
    /// Add a service
    /// </summary>
    /// <typeparam name="T">Type of service</typeparam>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithService<T>()
        where T : IService
    {
        return WithService(typeof(T));
    }

    /// <summary>
    /// Add a service
    /// </summary>
    /// <param name="serviceType">Type of service</param>
    /// <returns>Configuration builder instance</returns>
    [UnconditionalSuppressMessage("SingleFile", "IL3000:Avoid accessing Assembly file path when publishing as a single file", Justification = "Case is being handled")]
    public ConfigBuilder WithService(Type serviceType)
    {
        var serviceTypeLocation = serviceType.Assembly.Location;
        if (string.IsNullOrEmpty(serviceTypeLocation))
        {
            serviceTypeLocation = serviceType.Assembly.GetName().Name + ".dll";
        }

        // Check if services is already there.
        foreach (var existing in _configuration.Services)
        {
            if ((existing.FilePath == serviceTypeLocation || existing.InMemoryType == serviceType) &&
                existing.Type == serviceType.FullName)
            {
                return this;
            }
        }

        _configuration.Services.Add(new AssemblyLoadInfo
        {
            FilePath = serviceTypeLocation,
            Type = serviceType.FullName,
            InMemoryType = serviceType,
        });

        return this;
    }

    /// <summary>
    /// Add a service
    /// </summary>
    /// <typeparam name="T1">Type of service</typeparam>
    /// <typeparam name="T2">Type of service</typeparam>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithServices<T1, T2>()
        where T1 : IService
        where T2 : IService
    {
        return WithService(typeof(T1)).WithService(typeof(T2));
    }
    
    /// <summary>
    /// Add a service
    /// </summary>
    /// <typeparam name="T1">Type of service</typeparam>
    /// <typeparam name="T2">Type of service</typeparam>
    /// <typeparam name="T3">Type of service</typeparam>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithServices<T1, T2, T3>()
        where T1 : IService
        where T2 : IService
        where T3 : IService
    {
        return WithService(typeof(T1)).WithService(typeof(T2)).WithService(typeof(T3));
    }

    #endregion
    
    #region ProcessData

    /// <summary>
    /// Sets the process name
    /// </summary>
    /// <param name="processName">Process name to be executed</param>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithProcessName(string? processName)
    {
        _configuration.ProcessName = processName;
        return this;
    }

    /// <summary>
    /// Sets the process arguments
    /// </summary>
    /// <param name="processArguments">Process arguments</param>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithProcessArguments(string? processArguments)
    {
        _configuration.ProcessArguments = processArguments;
        return this;
    }
    
    /// <summary>
    /// Sets the working directory
    /// </summary>
    /// <param name="workingDirectory">Working directory</param>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithWorkingDirectory(string? workingDirectory)
    {
        _configuration.WorkingDirectory = workingDirectory;
        return this;
    }

    /// <summary>
    /// Adds process environment variables
    /// </summary>
    /// <param name="environmentVariables">Environment variables dictionary</param>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithEnvironmentVariables(Dictionary<string, string> environmentVariables)
    {
        foreach (var kv in environmentVariables)
        {
            _configuration.EnvironmentVariables[kv.Key] = kv.Value;
        }

        return this;
    }
    
    /// <summary>
    /// Adds a process environment variable
    /// </summary>
    /// <param name="name">Environment variable name</param>
    /// <param name="value">Environment variable value</param>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithEnvironmentVariable(string name, string value)
    {
        _configuration.EnvironmentVariables[name] = value;
        return this;
    }

    /// <summary>
    /// Adds paths to be validated before running an scenario
    /// </summary>
    /// <param name="pathValidations">Paths validations array</param>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithPathValidations(params string[] pathValidations)
    {
        _configuration.PathValidations.AddRange(pathValidations);
        return this;
    }

    /// <summary>
    /// Adds a path to be validated before running an scenario
    /// </summary>
    /// <param name="pathValidation">Path to be validated</param>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithPathValidations(string pathValidation)
    {
        _configuration.PathValidations.Add(pathValidation);
        return this;
    }

    /// <summary>
    /// Sets the timeout configuration
    /// </summary>
    /// <param name="timeoutBuilder">Timeout builder instance</param>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithTimeout(TimeoutBuilder timeoutBuilder)
    {
        _configuration.Timeout = timeoutBuilder.Build();
        return this;
    }

    /// <summary>
    /// Sets the timeout configuration
    /// </summary>
    /// <param name="timeoutBuilderFunc">Timeout builder delegate</param>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithTimeout(Func<TimeoutBuilder, TimeoutBuilder> timeoutBuilderFunc)
    {
        return WithTimeout(timeoutBuilderFunc(new TimeoutBuilder(_configuration.Timeout)));
    }
    
    /// <summary>
    /// Adds tags to each scenario
    /// </summary>
    /// <param name="tags">Tags dictionary</param>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithTags(Dictionary<string, string> tags)
    {
        foreach (var kv in tags)
        {
            _configuration.Tags[kv.Key] = kv.Value;
        }

        return this;
    }
    
    /// <summary>
    /// Adds a tag to each scenario
    /// </summary>
    /// <param name="key">Key of the tag</param>
    /// <param name="value">Value of the tag</param>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithTags(string key, string value)
    {
        _configuration.Tags[key] = value;
        return this;
    }

    /// <summary>
    /// Adds tags to each scenario
    /// </summary>
    /// <param name="tags">Tags dictionary</param>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithTags(Dictionary<string, object> tags)
    {
        foreach (var kv in tags)
        {
            _configuration.Tags[kv.Key] = kv.Value;
        }

        return this;
    }
    
    /// <summary>
    /// Adds a tag to each scenario
    /// </summary>
    /// <param name="key">Key of the tag</param>
    /// <param name="value">Value of the tag</param>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithTags(string key, IConvertible value)
    {
        _configuration.Tags[key] = value;
        return this;
    }
    #endregion
}
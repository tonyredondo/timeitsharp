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
        ArgumentNullException.ThrowIfNull(configuration);
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
    public Config Build()
    {
        EnsureConfigurationStructure();
        return _configuration;
    }

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
        EnsureConfigurationStructure();
        _configuration.EnableDatadog = enabled;
        if (enabled)
        {
            WithExporter<DatadogExporter>();
        }
        else
        {
            // Preserve the legacy switch semantics when a builder is reused: do not leave an
            // exporter declaration behind that the modern declaration-based resolver would treat
            // as an explicit Datadog enablement.
            _configuration.Exporters.RemoveAll(extension =>
                extension is not null && SelectsExtensionType(extension, typeof(DatadogExporter)));
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
        // Clear methods are repair operations: replace the collection so they also recover
        // from null collections and malformed entries without validating those entries first.
        _configuration.Scenarios = new();
        return this;
    }

    /// <summary>
    /// Adds a new scenario
    /// </summary>
    /// <param name="scenarioBuilder">ScenarioBuilder instance</param>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithScenario(ScenarioBuilder scenarioBuilder)
    {
        EnsureConfigurationStructure();
        ArgumentNullException.ThrowIfNull(scenarioBuilder);
        var scenario = scenarioBuilder.Build();
        if (scenario is null)
        {
            throw new ArgumentException("The scenario builder returned a null scenario.", nameof(scenarioBuilder));
        }

        _configuration.Scenarios.Add(scenario);
        return this;
    }
    
    /// <summary>
    /// Adds a new scenario
    /// </summary>
    /// <param name="scenarioBuilderFunc">Scenario builder delegate</param>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithScenario(Func<ScenarioBuilder, ScenarioBuilder> scenarioBuilderFunc)
    {
        EnsureConfigurationStructure();
        ArgumentNullException.ThrowIfNull(scenarioBuilderFunc);
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
        ArgumentNullException.ThrowIfNull(processName);
        _configuration.MetricsProcessName = processName;
        return this;
    }

    /// <summary>
    /// Sets the frequency in milliseconds to collect runtime metrics
    /// </summary>
    /// <param name="frequencyInMs">Frequency in Milliseconds</param>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithMetricsFrequency(int frequencyInMs)
    {
        _configuration.MetricsFrequencyInMs = frequencyInMs;
        return this;
    }
    
    /// <summary>
    /// Sets the name of the configuration
    /// </summary>
    /// <param name="name">Name of the configuration</param>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
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
        EnsureConfigurationStructure();
        ArgumentNullException.ThrowIfNull(filePath);
        _configuration.JsonExporterFilePath = filePath;
        if (!string.IsNullOrEmpty(filePath))
        {
            WithExporter<JsonExporter>();
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

    /// <summary>
    /// Sets the overhead threshold to fail an scenario if the overhead is greater than this value.
    /// </summary>
    /// <param name="overheadThreshold">Overhead threshold</param>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithOverheadThreshold(double overheadThreshold)
    {
        _configuration.OverheadThreshold = overheadThreshold;
        return this;
    }

    #region WithExporter

    /// <summary>Clears the exporters list.</summary>
    public ConfigBuilder ClearExporters()
    {
        _configuration.Exporters = new();
        _configuration.EnableDatadog = false;
        return this;
    }

    /// <summary>Adds an exporter declaration.</summary>
    public ConfigBuilder WithExporter(AssemblyLoadInfo exporter)
    {
        EnsureConfigurationStructure();
        return AddExtension(_configuration.Exporters, exporter, typeof(IExporter), "exporters");
    }

    /// <summary>Adds multiple exporter declarations.</summary>
    public ConfigBuilder WithExporter(params AssemblyLoadInfo[] exporters)
    {
        EnsureConfigurationStructure();
        ArgumentNullException.ThrowIfNull(exporters);
        foreach (var exporter in exporters)
        {
            AddExtension(_configuration.Exporters, exporter, typeof(IExporter), "exporters");
        }

        return this;
    }

    /// <summary>Adds a built-in or custom exporter by name.</summary>
    public ConfigBuilder WithExporter(string exporterName)
    {
        EnsureConfigurationStructure();
        ValidateName(exporterName, nameof(exporterName));
        AddExtension(
            _configuration.Exporters,
            new AssemblyLoadInfo { Name = exporterName },
            typeof(IExporter),
            "exporters");
        return this;
    }

    /// <summary>Adds a known exporter type.</summary>
    public ConfigBuilder WithExporter<T>() where T : IExporter => WithExporter(typeof(T));

    /// <summary>Adds a known exporter type.</summary>
    [UnconditionalSuppressMessage("SingleFile", "IL3000:Avoid accessing Assembly file path when publishing as a single file", Justification = "Case is being handled")]
    public ConfigBuilder WithExporter(Type exporterType)
    {
        EnsureConfigurationStructure();
        var info = CreateTypeLoadInfo<IExporter>(exporterType, nameof(exporterType));
        AddExtension(_configuration.Exporters, info, typeof(IExporter), "exporters");
        if (exporterType == typeof(DatadogExporter))
        {
            // Enabling is a property of selecting Datadog, not of whether the declaration was a
            // duplicate. This also fixes aliases that were already present in a JSON config.
            _configuration.EnableDatadog = true;
        }

        return this;
    }

    /// <summary>Adds two known exporter types.</summary>
    public ConfigBuilder WithExporters<T1, T2>()
        where T1 : IExporter
        where T2 : IExporter => WithExporter(typeof(T1)).WithExporter(typeof(T2));

    /// <summary>Adds three known exporter types.</summary>
    public ConfigBuilder WithExporters<T1, T2, T3>()
        where T1 : IExporter
        where T2 : IExporter
        where T3 : IExporter => WithExporter(typeof(T1)).WithExporter(typeof(T2)).WithExporter(typeof(T3));

    #endregion

    #region WithAssertor

    /// <summary>Clears the assertors list.</summary>
    public ConfigBuilder ClearAssertors()
    {
        _configuration.Assertors = new();
        return this;
    }

    /// <summary>Adds an assertor declaration.</summary>
    public ConfigBuilder WithAssertor(AssemblyLoadInfo assertor)
    {
        EnsureConfigurationStructure();
        return AddExtension(_configuration.Assertors, assertor, typeof(IAssertor), "assertors");
    }

    /// <summary>Adds multiple assertor declarations.</summary>
    public ConfigBuilder WithAssertor(params AssemblyLoadInfo[] assertors)
    {
        EnsureConfigurationStructure();
        ArgumentNullException.ThrowIfNull(assertors);
        foreach (var assertor in assertors)
        {
            AddExtension(_configuration.Assertors, assertor, typeof(IAssertor), "assertors");
        }

        return this;
    }

    /// <summary>Adds a built-in or custom assertor by name.</summary>
    public ConfigBuilder WithAssertor(string assertorName)
    {
        EnsureConfigurationStructure();
        ValidateName(assertorName, nameof(assertorName));
        AddExtension(
            _configuration.Assertors,
            new AssemblyLoadInfo { Name = assertorName },
            typeof(IAssertor),
            "assertors");
        return this;
    }

    /// <summary>Adds a known assertor type.</summary>
    public ConfigBuilder WithAssertor<T>() where T : IAssertor => WithAssertor(typeof(T));

    /// <summary>Adds a known assertor type.</summary>
    [UnconditionalSuppressMessage("SingleFile", "IL3000:Avoid accessing Assembly file path when publishing as a single file", Justification = "Case is being handled")]
    public ConfigBuilder WithAssertor(Type assertorType)
    {
        EnsureConfigurationStructure();
        var info = CreateTypeLoadInfo<IAssertor>(assertorType, nameof(assertorType));
        AddExtension(_configuration.Assertors, info, typeof(IAssertor), "assertors");
        return this;
    }

    /// <summary>Adds two known assertor types.</summary>
    public ConfigBuilder WithAssertors<T1, T2>()
        where T1 : IAssertor
        where T2 : IAssertor => WithAssertor(typeof(T1)).WithAssertor(typeof(T2));

    /// <summary>Adds three known assertor types.</summary>
    public ConfigBuilder WithAssertors<T1, T2, T3>()
        where T1 : IAssertor
        where T2 : IAssertor
        where T3 : IAssertor => WithAssertor(typeof(T1)).WithAssertor(typeof(T2)).WithAssertor(typeof(T3));

    #endregion

    #region WithService

    /// <summary>Clears the services list.</summary>
    public ConfigBuilder ClearServices()
    {
        _configuration.Services = new();
        return this;
    }

    /// <summary>Adds a service declaration.</summary>
    public ConfigBuilder WithService(AssemblyLoadInfo service)
    {
        EnsureConfigurationStructure();
        return AddExtension(_configuration.Services, service, typeof(IService), "services");
    }

    /// <summary>Adds multiple service declarations.</summary>
    public ConfigBuilder WithService(params AssemblyLoadInfo[] services)
    {
        EnsureConfigurationStructure();
        ArgumentNullException.ThrowIfNull(services);
        foreach (var service in services)
        {
            AddExtension(_configuration.Services, service, typeof(IService), "services");
        }

        return this;
    }

    /// <summary>Adds a built-in or custom service by name.</summary>
    public ConfigBuilder WithService(string serviceName)
    {
        EnsureConfigurationStructure();
        ValidateName(serviceName, nameof(serviceName));
        AddExtension(
            _configuration.Services,
            new AssemblyLoadInfo { Name = serviceName },
            typeof(IService),
            "services");
        return this;
    }

    /// <summary>Adds a known service type.</summary>
    public ConfigBuilder WithService<T>() where T : IService => WithService(typeof(T));

    /// <summary>Adds a known service type.</summary>
    [UnconditionalSuppressMessage("SingleFile", "IL3000:Avoid accessing Assembly file path when publishing as a single file", Justification = "Case is being handled")]
    public ConfigBuilder WithService(Type serviceType)
    {
        EnsureConfigurationStructure();
        var info = CreateTypeLoadInfo<IService>(serviceType, nameof(serviceType));
        AddExtension(_configuration.Services, info, typeof(IService), "services");
        return this;
    }

    /// <summary>Adds two known service types.</summary>
    public ConfigBuilder WithServices<T1, T2>()
        where T1 : IService
        where T2 : IService => WithService(typeof(T1)).WithService(typeof(T2));

    /// <summary>Adds three known service types.</summary>
    public ConfigBuilder WithServices<T1, T2, T3>()
        where T1 : IService
        where T2 : IService
        where T3 : IService => WithService(typeof(T1)).WithService(typeof(T2)).WithService(typeof(T3));

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
        EnsureConfigurationStructure();
        ArgumentNullException.ThrowIfNull(environmentVariables);
        foreach (var kv in environmentVariables)
        {
            ValidateName(kv.Key, "environmentVariables key");
            ArgumentNullException.ThrowIfNull(kv.Value);
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
        EnsureConfigurationStructure();
        ValidateName(name, nameof(name));
        ArgumentNullException.ThrowIfNull(value);
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
        EnsureConfigurationStructure();
        ArgumentNullException.ThrowIfNull(pathValidations);
        foreach (var pathValidation in pathValidations)
        {
            ValidateRequiredText(pathValidation, "pathValidation");
            _configuration.PathValidations.Add(pathValidation);
        }

        return this;
    }

    /// <summary>
    /// Adds a path to be validated before running an scenario
    /// </summary>
    /// <param name="pathValidation">Path to be validated</param>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithPathValidations(string pathValidation)
    {
        EnsureConfigurationStructure();
        ValidateRequiredText(pathValidation, nameof(pathValidation));
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
        EnsureConfigurationStructure();
        ArgumentNullException.ThrowIfNull(timeoutBuilder);
        var timeout = timeoutBuilder.Build();
        if (timeout is null)
        {
            throw new ArgumentException("The timeout builder returned a null timeout.", nameof(timeoutBuilder));
        }

        _configuration.Timeout = timeout;
        return this;
    }

    /// <summary>
    /// Sets the timeout configuration
    /// </summary>
    /// <param name="timeoutBuilderFunc">Timeout builder delegate</param>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithTimeout(Func<TimeoutBuilder, TimeoutBuilder> timeoutBuilderFunc)
    {
        EnsureConfigurationStructure();
        ArgumentNullException.ThrowIfNull(timeoutBuilderFunc);
        return WithTimeout(timeoutBuilderFunc(new TimeoutBuilder(_configuration.Timeout)));
    }
    
    /// <summary>
    /// Adds tags to each scenario
    /// </summary>
    /// <param name="tags">Tags dictionary</param>
    /// <returns>Configuration builder instance</returns>
    public ConfigBuilder WithTags(Dictionary<string, string> tags)
    {
        EnsureConfigurationStructure();
        ArgumentNullException.ThrowIfNull(tags);
        foreach (var kv in tags)
        {
            ValidateName(kv.Key, "tags key");
            ArgumentNullException.ThrowIfNull(kv.Value);
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
        EnsureConfigurationStructure();
        ValidateName(key, nameof(key));
        ArgumentNullException.ThrowIfNull(value);
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
        EnsureConfigurationStructure();
        ArgumentNullException.ThrowIfNull(tags);
        foreach (var kv in tags)
        {
            ValidateName(kv.Key, "tags key");
            ArgumentNullException.ThrowIfNull(kv.Value);
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
        EnsureConfigurationStructure();
        ValidateName(key, nameof(key));
        ArgumentNullException.ThrowIfNull(value);
        _configuration.Tags[key] = value;
        return this;
    }
    #endregion

    private void EnsureConfigurationStructure()
    {
        _configuration.ValidateStructure();
    }

    private ConfigBuilder AddExtension(
        List<AssemblyLoadInfo> extensions,
        AssemblyLoadInfo extension,
        Type extensionContract,
        string propertyName)
    {
        ArgumentNullException.ThrowIfNull(extensions);
        ArgumentNullException.ThrowIfNull(extension);
        ArgumentNullException.ThrowIfNull(extensionContract);

        var errors = Config.GetAssemblyLoadInfoValidationErrors(
            extension,
            $"{propertyName}[{extensions.Count}]");
        if (errors.Count > 0)
        {
            throw new ArgumentException(string.Join(Environment.NewLine, errors), nameof(extension));
        }

        if (extension.InMemoryType is { } inMemoryType && !extensionContract.IsAssignableFrom(inMemoryType))
        {
            throw new ArgumentException(
                $"{propertyName} entry type '{inMemoryType.FullName ?? inMemoryType.Name}' does not implement {extensionContract.FullName}.",
                nameof(extension));
        }

        if (!extensions.Any(existing =>
                ExtensionIdentity.AreEquivalent(existing, extension, extensionContract, _configuration.Path)))
        {
            extensions.Add(extension);
        }

        if (extensionContract == typeof(IExporter) &&
            SelectsExtensionType(extension, typeof(DatadogExporter)))
        {
            _configuration.EnableDatadog = true;
        }

        return this;
    }

    private static bool SelectsExtensionType(AssemblyLoadInfo extension, Type extensionType)
    {
        if (extension.InMemoryType is not null)
        {
            return extension.InMemoryType == extensionType;
        }

        if (!string.IsNullOrWhiteSpace(extension.FilePath))
        {
            return string.Equals(extension.Type, extensionType.FullName, StringComparison.Ordinal);
        }

        return BuiltInExtensionAliases.IsAlias(extensionType, extension.Name);
    }

    [UnconditionalSuppressMessage("SingleFile", "IL3000:Avoid accessing Assembly file path when publishing as a single file", Justification = "Case is being handled")]
    private static AssemblyLoadInfo CreateTypeLoadInfo<T>(Type extensionType, string parameterName)
        where T : class, INamedExtension
    {
        ArgumentNullException.ThrowIfNull(extensionType);
        if (!typeof(T).IsAssignableFrom(extensionType))
        {
            throw new ArgumentException(
                $"Type '{extensionType.FullName ?? extensionType.Name}' must implement {typeof(T).FullName}.",
                parameterName);
        }

        if (extensionType.IsAbstract || extensionType.IsInterface || extensionType.IsEnum)
        {
            throw new ArgumentException(
                $"Type '{extensionType.FullName ?? extensionType.Name}' cannot be used as an extension.",
                parameterName);
        }

        var typeName = extensionType.FullName;
        if (string.IsNullOrWhiteSpace(typeName))
        {
            throw new ArgumentException("An extension type must have a full name.", parameterName);
        }

        var typeLocation = extensionType.Assembly.Location;
        if (string.IsNullOrEmpty(typeLocation))
        {
            typeLocation = extensionType.Assembly.GetName().Name + ".dll";
        }

        return new AssemblyLoadInfo
        {
            FilePath = typeLocation,
            Type = typeName,
            InMemoryType = extensionType,
        };
    }

    private static void ValidateName(string name, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(name, parameterName);
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A name is required.", parameterName);
        }
    }

    private static void ValidateRequiredText(string value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-empty value is required.", parameterName);
        }
    }

}
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Loader;
using Spectre.Console;
using TimeItSharp.Common.Assertors;
using TimeItSharp.Common.Configuration;
using TimeItSharp.Common.Configuration.Builder;
using TimeItSharp.Common.Exporters;
using TimeItSharp.Common.Results;
using TimeItSharp.Common.Services;
using Status = TimeItSharp.Common.Results.Status;

namespace TimeItSharp.Common;

public static class TimeItEngine
{
    /// <summary>
    /// Runs TimeIt
    /// </summary>
    /// <param name="configurationFile">Configuration file to be executed</param>
    /// <param name="options">TimeIt options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Exit code of the TimeIt engine</returns>
    [RequiresUnreferencedCode("")]
    public static Task<int> RunAsync(string configurationFile, TimeItOptions? options = null, CancellationToken? cancellationToken = null)
    {
        // Load configuration
        var config = Config.LoadConfiguration(configurationFile);
        return RunAsync(config, options, cancellationToken);
    }

    /// <summary>
    /// Runs TimeIt
    /// </summary>
    /// <param name="configBuilder">Configuration builder instance to be executed</param>
    /// <param name="options">TimeIt options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Exit code of the TimeIt engine</returns>
    [RequiresUnreferencedCode("")]
    public static Task<int> RunAsync(ConfigBuilder configBuilder, TimeItOptions? options = null, CancellationToken? cancellationToken = null)
    {
        return RunAsync(configBuilder.Build(), options, cancellationToken);
    }

    /// <summary>
    /// Runs TimeIt
    /// </summary>
    /// <param name="config">Configuration instance to be executed</param>
    /// <param name="options">TimeIt options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Exit code of the TimeIt engine</returns>
    [RequiresUnreferencedCode("")]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor, typeof(DatadogProfilerService))]
    public static async Task<int> RunAsync(Config config, TimeItOptions? options = null, CancellationToken? cancellationToken = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        // Validate before cloning: Clone assumes all collections and nested process data are
        // present, and a malformed JSON document should result in a useful configuration
        // error instead of an unrelated NullReferenceException.
        config.Validate();
        config = config.Clone();
        options ??= new TimeItOptions(new TemplateVariables());
        cancellationToken ??= CancellationToken.None;
        var templateVariables = options.TemplateVariables ?? new TemplateVariables();
        var statesByType = options.StatesByType;
        var timeitCallbacks = new TimeItCallbacks();
        var callbacksTriggers = timeitCallbacks.GetTriggers();
        // The trigger object exists even when extension loading or initialization fails. Finish
        // callbacks must still run so services can release resources they acquired earlier.
        var callbacksInitialized = true;
        var scenariosResults = new List<ScenarioResult>();
        var scenarioWithErrors = 0;
        var callbacksStarted = false;
        var exporterErrors = 0;
        var lifecycleErrors = false;
        var afterAllScenariosCalled = false;
        ScenarioProcessor? processor = null;
        var engineExitCode = 0;

        try
        {
            // Prepare configuration before extensions are initialized.
            config.JsonExporterFilePath = templateVariables.Expand(config.JsonExporterFilePath);
            ExpandAssemblyLoadInfoValues(config.Exporters, templateVariables);
            ExpandAssemblyLoadInfoValues(config.Assertors, templateVariables);
            ExpandAssemblyLoadInfoValues(config.Services, templateVariables);

            // Keep extension loading and initialization inside the lifecycle guard. A custom
            // service can subscribe callbacks and then fail during initialization; those
            // callbacks still need a chance to finish in the finally block.
            var exportersInfo = GetFromAssemblyLoadInfoList(
                config.Exporters,
                () => new List<IExporter> { new ConsoleExporter(), new JsonExporter(), new DatadogExporter() });
            var exporters = exportersInfo.Select(i => i.Instance).ToList();

            var assertorsInfo = GetFromAssemblyLoadInfoList(
                config.Assertors,
                () => new List<IAssertor> { new DefaultAssertor() });
            // Enabled can depend on InitOptions (for example, a custom assertor may read a
            // configuration flag in Initialize), so initialize every loaded assertor before
            // selecting the instances that participate in scenario processing.
            foreach (var assertor in assertorsInfo)
            {
                var state = statesByType.GetValueOrDefault(assertor.Instance.GetType());
                assertor.Instance.Initialize(new InitOptions(config, assertor.LoadInfo, templateVariables, state));
            }

            var assertors = assertorsInfo
                .Where(i => i.Instance.Enabled)
                .Select(i => i.Instance)
                .ToList();

            var servicesInfo = GetFromAssemblyLoadInfoList<IService>(
                config.Services,
                () => new List<IService> { new NoopService() });
            var services = servicesInfo.Select(i => i.Instance).ToList();
            foreach (var service in servicesInfo)
            {
                var state = statesByType.GetValueOrDefault(service.Instance.GetType());
                service.Instance.Initialize(new InitOptions(config, service.LoadInfo, templateVariables, state), timeitCallbacks);
            }

            processor = new ScenarioProcessor(config, templateVariables, assertors, services, callbacksTriggers);

            AnsiConsole.Profile.Width = Utils.GetSafeWidth();
            AnsiConsole.MarkupLine("[bold aqua]Warmup count:[/] {0}", config.WarmUpCount);
            AnsiConsole.MarkupLine("[bold aqua]Max count:[/] {0}", config.Count);
            AnsiConsole.MarkupLine("[bold aqua]Acceptable relative width:[/] {0}%", Math.Round(config.AcceptableRelativeWidth * 100, 2));
            AnsiConsole.MarkupLine("[bold aqua]Confidence level:[/] {0}%", Math.Round(config.ConfidenceLevel * 100, 2));
            AnsiConsole.MarkupLine("[bold aqua]Minimum error reduction:[/] {0}%", Math.Round(config.MinimumErrorReduction * 100, 2));
            AnsiConsole.MarkupLine("[bold aqua]Maximum duration:[/] {0}min", config.MaximumDurationInMinutes);
            if (config.OverheadThreshold > 0)
            {
                AnsiConsole.MarkupLine("[bold aqua]Overhead threshold:[/] {0}%", Math.Round(config.OverheadThreshold * 100, 2));
            }

            AnsiConsole.MarkupLine("[bold aqua]Number of Scenarios:[/] {0}", config.Scenarios.Count);
            AnsiConsole.MarkupLine("[bold aqua]Exporters:[/] {0}", string.Join(", ", exporters.Select(e => e.Name)));
            AnsiConsole.MarkupLine("[bold aqua]Assertors:[/] {0}", string.Join(", ", assertors.Select(e => e.Name)));
            AnsiConsole.MarkupLine("[bold aqua]Services:[/] {0}", string.Join(", ", services.Select(e => e.Name)));
            AnsiConsole.WriteLine();

            if (config is { Count: > 0, Scenarios.Count: > 0 })
            {
                if (config.Scenarios.Any(s => s.IsBaseline))
                {
                    config.Scenarios = config.Scenarios.OrderByDescending(s => s.IsBaseline).ToList();
                }

                callbacksStarted = true;
                callbacksTriggers.BeforeAllScenariosStarts(config.Scenarios);
                for (var i = 0; i < config.Scenarios.Count; i++)
                {
                    var scenario = config.Scenarios[i];

                    processor.PrepareScenario(scenario);
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    var result = await processor.ProcessScenarioAsync(
                        i,
                        scenario,
                        cancellationToken: cancellationToken.Value).ConfigureAwait(false);

                    if (result is null || cancellationToken.Value.IsCancellationRequested)
                    {
                        scenarioWithErrors++;
                        break;
                    }

                    if (result.Status != Status.Passed)
                    {
                        scenarioWithErrors++;
                    }

                    scenariosResults.Add(result);
                }

                GC.Collect();
                GC.WaitForPendingFinalizers();
                try
                {
                    afterAllScenariosCalled = true;
                    callbacksTriggers.AfterAllScenariosFinishes(scenariosResults);
                }
                catch (Exception ex)
                {
                    lifecycleErrors = true;
                    AnsiConsole.MarkupLine("[red]Error running AfterAllScenariosFinishes:[/]");
                    AnsiConsole.WriteLine(ex.ToString());
                }

                var results = new TimeitResult
                {
                    Scenarios = scenariosResults,
                    Overheads = Utils.GetComparisonTableData(scenariosResults),
                };

                foreach (var exporter in exportersInfo)
                {
                    try
                    {
                        var state = statesByType.GetValueOrDefault(exporter.Instance.GetType());
                        exporter.Instance.Initialize(new InitOptions(config, exporter.LoadInfo, templateVariables, state));
                        if (exporter.Instance.Enabled)
                        {
                            exporter.Instance.Export(results);
                        }
                    }
                    catch (Exception ex)
                    {
                        exporterErrors++;
                        AnsiConsole.MarkupLine("[red]Error running exporter '{0}':[/]", exporter.Instance.Name);
                        AnsiConsole.WriteLine(ex.ToString());
                    }
                }
            }

            engineExitCode = cancellationToken.Value.IsCancellationRequested || scenarioWithErrors > 0 || exporterErrors > 0 ? 1 : 0;
        }
        finally
        {
            if (processor is not null)
            {
                foreach (var scenario in config.Scenarios)
                {
                    try
                    {
                        processor.CleanScenario(scenario);
                    }
                    catch (Exception ex)
                    {
                        lifecycleErrors = true;
                        AnsiConsole.MarkupLine("[red]Error cleaning scenario:[/]");
                        AnsiConsole.WriteLine(ex.ToString());
                    }
                }
            }

            if (callbacksStarted && !afterAllScenariosCalled)
            {
                try
                {
                    afterAllScenariosCalled = true;
                    callbacksTriggers.AfterAllScenariosFinishes(scenariosResults);
                }
                catch (Exception ex)
                {
                    lifecycleErrors = true;
                    AnsiConsole.MarkupLine("[red]Error running AfterAllScenariosFinishes:[/]");
                    AnsiConsole.WriteLine(ex.ToString());
                }
            }

            if (callbacksInitialized)
            {
                try
                {
                    callbacksTriggers.Finish();
                }
                catch (Exception ex)
                {
                    lifecycleErrors = true;
                    AnsiConsole.MarkupLine("[red]Error running OnFinish:[/]");
                    AnsiConsole.WriteLine(ex.ToString());
                }
            }
        }

        return lifecycleErrors ? 1 : engineExitCode;
    }

    private static void ExpandAssemblyLoadInfoValues(
        IReadOnlyList<AssemblyLoadInfo> assemblyLoadInfos,
        TemplateVariables templateVariables)
    {
        foreach (var info in assemblyLoadInfos)
        {
            if (info is null)
            {
                continue;
            }

            if (info.FilePath is { } filePath)
            {
                info.FilePath = templateVariables.Expand(filePath);
            }

            if (info.Type is { } type)
            {
                info.Type = templateVariables.Expand(type);
            }

            if (info.Name is { } name)
            {
                info.Name = templateVariables.Expand(name);
            }
        }
    }

    [RequiresUnreferencedCode("Calls System.Runtime.Loader.AssemblyLoadContext.LoadFromAssemblyPath(String)")]
    private static List<(T Instance, AssemblyLoadInfo? LoadInfo)> GetFromAssemblyLoadInfoList<T>(
        IReadOnlyList<AssemblyLoadInfo> assemblyLoadInfos,
        Func<List<T>>? defaultListFunc = null)
        where T : class, INamedExtension
    {
        if (assemblyLoadInfos is null || assemblyLoadInfos.Count == 0)
        {
            return (defaultListFunc?.Invoke() ?? new List<T>())
                .Select(i => (i, (AssemblyLoadInfo?)null))
                .ToList();
        }

        var resultList = new List<(T, AssemblyLoadInfo?)>();
        var loadContext = AssemblyLoadContext.Default;
        foreach (var assemblyLoadInfo in assemblyLoadInfos)
        {
            if (assemblyLoadInfo is null)
            {
                throw new InvalidOperationException($"A {typeof(T).Name} extension entry cannot be null.");
            }

            try
            {
                T? instance = default;
                if (assemblyLoadInfo.InMemoryType is { } inMemoryType)
                {
                    instance = Activator.CreateInstance(inMemoryType) as T;
                }
                else if (!string.IsNullOrWhiteSpace(assemblyLoadInfo.FilePath))
                {
                    if (string.IsNullOrWhiteSpace(assemblyLoadInfo.Type))
                    {
                        throw new InvalidOperationException("An extension type is required when filePath is specified.");
                    }

                    var assemblyPath = Path.GetFullPath(assemblyLoadInfo.FilePath);
                    if (!File.Exists(assemblyPath))
                    {
                        throw new FileNotFoundException("Extension assembly not found.", assemblyPath);
                    }

                    var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);
                    if (assembly.GetType(assemblyLoadInfo.Type, throwOnError: true) is not { } type)
                    {
                        throw new TypeLoadException($"Type '{assemblyLoadInfo.Type}' was not found in '{assemblyPath}'.");
                    }

                    instance = Activator.CreateInstance(type) as T;
                }
                else if (!string.IsNullOrWhiteSpace(assemblyLoadInfo.Name))
                {
                    Exception? activationError = null;
                    foreach (var assembly in loadContext.Assemblies)
                    {
                        TypeInfo[] definedTypes;
                        try
                        {
                            definedTypes = assembly.DefinedTypes.ToArray();
                        }
                        catch (ReflectionTypeLoadException ex)
                        {
                            definedTypes = ex.Types
                                .Where(type => type is not null)
                                .Select(type => type!.GetTypeInfo())
                                .ToArray();
                        }

                        foreach (var typeInfo in definedTypes)
                        {
                            if (typeInfo.IsAbstract || typeInfo.IsInterface || typeInfo.IsEnum ||
                                !typeof(T).IsAssignableFrom(typeInfo.AsType()))
                            {
                                continue;
                            }

                            try
                            {
                                if (Activator.CreateInstance(typeInfo.AsType()) is T candidate &&
                                    string.Equals(candidate.Name, assemblyLoadInfo.Name, StringComparison.Ordinal))
                                {
                                    instance = candidate;
                                    break;
                                }
                            }
                            catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException)
                            {
                                // An unrelated extension type can have unavailable dependencies or a
                                // constructor that is not usable in this process. Continue searching,
                                // then report the activation failure if no matching name is found.
                                activationError ??= ex;
                            }
                        }

                        if (instance is not null)
                        {
                            break;
                        }
                    }

                    if (instance is null && activationError is not null)
                    {
                        throw new InvalidOperationException(
                            $"Could not create {typeof(T).Name} extension named '{assemblyLoadInfo.Name}'.",
                            activationError);
                    }
                }
                else
                {
                    throw new InvalidOperationException("An extension entry must specify name, filePath/type, or an in-memory type.");
                }

                if (instance is null)
                {
                    throw new InvalidOperationException(
                        $"Could not create {typeof(T).Name} extension '{assemblyLoadInfo.Name ?? assemblyLoadInfo.Type}'.");
                }

                resultList.Add((instance, assemblyLoadInfo));
            }
            catch (Exception ex) when (ex is not InvalidOperationException && ex is not FileNotFoundException)
            {
                throw new InvalidOperationException(
                    $"Could not load {typeof(T).Name} extension '{assemblyLoadInfo.Name ?? assemblyLoadInfo.Type ?? assemblyLoadInfo.FilePath}'.",
                    ex);
            }
        }

        return resultList;
    }

}

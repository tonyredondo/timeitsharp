using System.Diagnostics.CodeAnalysis;
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
    public static async Task<int> RunAsync(Config config, TimeItOptions? options = null, CancellationToken? cancellationToken = null)
    {
        config = config.Clone();
        options ??= new TimeItOptions(new TemplateVariables());
        cancellationToken ??= CancellationToken.None;
        var templateVariables = options.TemplateVariables ?? new TemplateVariables();
        var statesByType = options.StatesByType;

        // Prepare configuration
        config.JsonExporterFilePath = templateVariables.Expand(config.JsonExporterFilePath);

        // Exporters
        var exportersInfo = GetFromAssemblyLoadInfoList<IExporter>(
            config.Exporters,
            () =>
            {
                if (config.EnableDatadog)
                {
                    return [new ConsoleExporter(), new JsonExporter(), new DatadogExporter()];
                }

                return [new ConsoleExporter(), new JsonExporter()];
            });
        var exporters = exportersInfo.Select(i => i.Instance).ToList();
    
        // Assertors
        var assertorsInfo = GetFromAssemblyLoadInfoList(
            config.Assertors,
            () => new List<IAssertor> { new DefaultAssertor() });
        var assertors = assertorsInfo.Select(i => i.Instance).ToList();
        foreach (var assertor in assertorsInfo)
        {
            var state = statesByType.GetValueOrDefault(assertor.Instance.GetType());
            assertor.Instance.Initialize(new InitOptions(config, assertor.LoadInfo, templateVariables, state));
        }

        // Services
        var timeitCallbacks = new TimeItCallbacks();
        var callbacksTriggers = timeitCallbacks.GetTriggers();
        var servicesInfo = GetFromAssemblyLoadInfoList<IService>(config.Services, () => new List<IService> { new NoopService() });
        var services = servicesInfo.Select(i => i.Instance).ToList();
        foreach (var service in servicesInfo)
        {
            var state = statesByType.GetValueOrDefault(service.Instance.GetType());
            service.Instance.Initialize(new InitOptions(config, service.LoadInfo, templateVariables, state), timeitCallbacks);
        }

        // Create scenario processor
        var processor = new ScenarioProcessor(config, templateVariables, assertors, services, callbacksTriggers);

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

        // Process scenarios
        var scenariosResults = new List<ScenarioResult>();
        var scenarioWithErrors = 0;
        if (config is { Count: > 0, Scenarios.Count: > 0 })
        {
            if (config.Scenarios.Any(s => s.IsBaseline))
            {
                config.Scenarios = config.Scenarios.OrderByDescending(s => s.IsBaseline).ToList();
            }

            callbacksTriggers.BeforeAllScenariosStarts(config.Scenarios);
            for(var i = 0; i < config.Scenarios.Count; i++)
            {
                var scenario = config.Scenarios[i];

                // Prepare scenario
                processor.PrepareScenario(scenario);

                // Process scenario
                GC.Collect();
                GC.WaitForPendingFinalizers();
                var result = await processor.ProcessScenarioAsync(i, scenario, cancellationToken: cancellationToken.Value).ConfigureAwait(false);
                if (cancellationToken.Value.IsCancellationRequested)
                {
                    return 1;
                }

                if (result is null || result.Status != Status.Passed)
                {
                    scenarioWithErrors++;
                }

                if (result is not null)
                {
                    scenariosResults.Add(result);
                }
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            callbacksTriggers.AfterAllScenariosFinishes(scenariosResults);

            var results = new TimeitResult
            {
                Scenarios = scenariosResults,
                Overheads = Utils.GetComparisonTableData(scenariosResults),
            };

            // Export data
            foreach (var exporter in exportersInfo)
            {
                var state = statesByType.GetValueOrDefault(exporter.Instance.GetType());
                exporter.Instance.Initialize(new InitOptions(config, exporter.LoadInfo, templateVariables, state));
                if (exporter.Instance.Enabled)
                {
                    exporter.Instance.Export(results);
                }
            }

            // Clean scenarios
            foreach (var scenario in config.Scenarios)
            {
                processor.CleanScenario(scenario);
            }

            callbacksTriggers.Finish();

            if (scenarioWithErrors > 0)
            {
                return 1;
            }
        }

        return 0;
    }

    [RequiresUnreferencedCode("Calls System.Runtime.Loader.AssemblyLoadContext.LoadFromAssemblyPath(String)")]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(ConsoleExporter))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(JsonExporter))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(DatadogExporter))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(DefaultAssertor))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(DatadogProfilerService))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(ExecuteService))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(NoopService))]
    private static List<(T Instance, AssemblyLoadInfo? LoadInfo)> GetFromAssemblyLoadInfoList<T>(
        IReadOnlyList<AssemblyLoadInfo> assemblyLoadInfos,
        Func<List<T>>? defaultListFunc = null)
        where T : INamedExtension
    {
        if (assemblyLoadInfos is null || assemblyLoadInfos.Count == 0)
        {
            return (defaultListFunc?.Invoke() ?? new List<T>()).Select(i => (i, (AssemblyLoadInfo?)null)).ToList();
        }
        
        var resultList = new List<(T, AssemblyLoadInfo?)>();
        var loadContext = AssemblyLoadContext.Default;
        foreach (var assemblyLoadInfo in assemblyLoadInfos)
        {
            if (assemblyLoadInfo is null)
            {
                continue;
            }

            if (assemblyLoadInfo.InMemoryType is { } inMemoryType)
            {
                if (Activator.CreateInstance(inMemoryType) is T instance)
                {
                    resultList.Add((instance, assemblyLoadInfo));
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]Error creating {0}[/]: {1}", typeof(T).Name,
                        inMemoryType.FullName ?? string.Empty);
                }

                continue;
            }

            if (!string.IsNullOrEmpty(assemblyLoadInfo.FilePath))
            {
                var assembly = loadContext.LoadFromAssemblyPath(assemblyLoadInfo.FilePath);
                if (!string.IsNullOrEmpty(assemblyLoadInfo.Type))
                {
                    if (assembly.GetType(assemblyLoadInfo.Type, throwOnError: true) is { } type)
                    {
                        if (Activator.CreateInstance(type) is T instance)
                        {
                            resultList.Add((instance, assemblyLoadInfo));
                        }
                        else
                        {
                            AnsiConsole.MarkupLine("[red]Error creating {0}[/]: {1}", typeof(T).Name,
                                type.FullName ?? string.Empty);
                        }
                    }
                }
            }
            else if (!string.IsNullOrEmpty(assemblyLoadInfo.Name))
            {
                if (typeof(T) == typeof(IExporter))
                {
                    bool LookForExporters<EType>(string name) where EType : IExporter, new()
                    {
                        if (assemblyLoadInfo.Name == name)
                        {
                            resultList.Add(((T)(object)new EType(), assemblyLoadInfo));
                            return true;
                        }

                        return false;
                    }
                    
                    if (LookForExporters<ConsoleExporter>(nameof(ConsoleExporter)) ||
                        LookForExporters<JsonExporter>(nameof(JsonExporter)) ||
                        LookForExporters<DatadogExporter>(nameof(DatadogExporter)) ||
                        LookForExporters<DatadogExporter>("Datadog"))
                    {
                        goto found_and_added;
                    }
                }
                
                if (typeof(T) == typeof(IAssertor))
                {
                    bool LookForAssertors<AType>(string name) where AType : IAssertor, new()
                    {
                        if (assemblyLoadInfo.Name == name)
                        {
                            resultList.Add(((T)(object)new AType(), assemblyLoadInfo));
                            return true;
                        }

                        return false;
                    }
                    
                    if (LookForAssertors<DefaultAssertor>("DefaultAssertor"))
                    {
                        goto found_and_added;
                    }
                }
                
                if (typeof(T) == typeof(IService))
                {
                    bool LookForServices<SType>(string name) where SType : IService, new()
                    {
                        if (assemblyLoadInfo.Name == name)
                        {
                            resultList.Add(((T)(object)new SType(), assemblyLoadInfo));
                            return true;
                        }

                        return false;
                    }
                    
                    if (LookForServices<DatadogProfilerService>(nameof(DatadogProfilerService)) ||
                        LookForServices<DatadogProfilerService>("DatadogProfiler") ||
                        LookForServices<ExecuteService>(nameof(ExecuteService)) ||
                        LookForServices<ExecuteService>("Execute") ||
                        LookForServices<NoopService>(nameof(NoopService)))
                    {
                        goto found_and_added;
                    }
                }
                
                foreach (var assembly in loadContext.Assemblies)
                {
                    foreach (var typeInfo in assembly.DefinedTypes)
                    {
                        if (typeInfo.IsAbstract || typeInfo.IsInterface || typeInfo.IsEnum)
                        {
                            continue;
                        }

                        foreach (var iface in typeInfo.ImplementedInterfaces)
                        {
                            if (iface is null)
                            {
                                continue;
                            }

                            if (iface.FullName == typeof(T).FullName)
                            {
                                if (Activator.CreateInstance(typeInfo) is T instance &&
                                    instance.Name == assemblyLoadInfo.Name)
                                {
                                    resultList.Add((instance, assemblyLoadInfo));
                                    // Let's exit the 3 nested foreach loops
                                    goto found_and_added;
                                }
                            }
                        }
                    }
                }

                AnsiConsole.MarkupLine("[red]Error creating {0}[/]: {1} - Not found", typeof(T).Name, assemblyLoadInfo.Name);
                
                found_and_added:
                {
                }
            }
        }

        return resultList;
    }
}
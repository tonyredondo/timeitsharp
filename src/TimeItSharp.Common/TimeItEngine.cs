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
    public static Task<int> RunAsync(string configurationFile, TemplateVariables? templateVariables = null)
    {
        // Load configuration
        var config = Config.LoadConfiguration(configurationFile);
        return RunAsync(config, templateVariables);
    }
    
    public static Task<int> RunAsync(ConfigBuilder configBuilder, TemplateVariables? templateVariables = null)
    {
        return RunAsync(configBuilder.Build(), templateVariables);
    }

    public static async Task<int> RunAsync(Config config, TemplateVariables? templateVariables = null)
    {
        templateVariables ??= new TemplateVariables();

        // Prepare configuration
        config.JsonExporterFilePath = templateVariables.Expand(config.JsonExporterFilePath);

        // Exporters
        var exporters = GetFromAssemblyLoadInfoList(
            config.Exporters,
            () => new List<IExporter> { new ConsoleExporter(), new JsonExporter(), new DatadogExporter() });
    
        // Assertors
        var assertors = GetFromAssemblyLoadInfoList(
            config.Assertors,
            () => new List<IAssertor> { new DefaultAssertor() });
        foreach (var assertor in assertors)
        {
            assertor.SetConfiguration(config);
        }

        // Services
        var timeitCallbacks = new TimeItCallbacks();
        var callbacksTriggers = timeitCallbacks.GetTriggers();
        var services = GetFromAssemblyLoadInfoList<IService>(config.Services);
        foreach (var service in services)
        {
            service.Initialize(config, timeitCallbacks);
        }

        // Create scenario processor
        var processor = new ScenarioProcessor(config, templateVariables, assertors, services, callbacksTriggers);

        AnsiConsole.MarkupLine("[bold aqua]Warmup count:[/] {0}", config.WarmUpCount);
        AnsiConsole.MarkupLine("[bold aqua]Count:[/] {0}", config.Count);
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
            for(var i = 0; i < config.Scenarios.Count; i++)
            {
                var scenario = config.Scenarios[i];

                // Prepare scenario
                processor.PrepareScenario(scenario);

                // Process scenario
                var result = await processor.ProcessScenarioAsync(i, scenario).ConfigureAwait(false);
                if (result.Status != Status.Passed)
                {
                    scenarioWithErrors++;
                }

                scenariosResults.Add(result);
            }

            // Export data
            foreach (var exporter in exporters)
            {
                exporter.SetConfiguration(config);
                if (exporter.Enabled)
                {
                    exporter.Export(scenariosResults);
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

    private static List<T> GetFromAssemblyLoadInfoList<T>(
        IReadOnlyList<AssemblyLoadInfo> assemblyLoadInfos,
        Func<List<T>>? defaultListFunc = null)
        where T : INamedExtension
    {
        if (assemblyLoadInfos is null || assemblyLoadInfos.Count == 0)
        {
            return defaultListFunc?.Invoke() ?? new List<T>();
        }
        
        var resultList = new List<T>();
        var loadContext = AssemblyLoadContext.Default;
        foreach (var assemblyLoadInfo in assemblyLoadInfos)
        {
            if (assemblyLoadInfo is null)
            {
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
                            resultList.Add(instance);
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
                                    resultList.Add(instance);
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
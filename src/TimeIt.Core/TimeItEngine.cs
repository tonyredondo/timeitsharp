using System.Runtime.Loader;
using Spectre.Console;
using TimeIt.Common.Assertors;
using TimeIt.Common.Configuration;
using TimeIt.Common.Exporters;
using TimeIt.Common.Results;
using TimeIt.Common.Services;
using TimeIt.DatadogExporter;
using Status = TimeIt.Common.Results.Status;

namespace TimeIt.Core;

public static class TimeItEngine
{
    public static Task<int> RunAsync(string configurationFile, TemplateVariables? templateVariables = null)
    {
        // Load configuration
        var config = Config.LoadConfiguration(configurationFile);
        return RunAsync(config, templateVariables);
    }

    public static async Task<int> RunAsync(Config config, TemplateVariables? templateVariables = null)
    {
        templateVariables ??= new TemplateVariables();

        // Prepare configuration
        config.JsonExporterFilePath = templateVariables.Expand(config.JsonExporterFilePath);

        // Exporters
        var exporters = new List<IExporter>();
        exporters.Add(new ConsoleExporter());
        exporters.Add(new JsonExporter());
        exporters.Add(new TimeItDatadogExporter());
    
        // Assertors
        var assertors = GetAssertors(config);
        foreach (var assertor in assertors)
        {
            assertor.SetConfiguration(config);
        }

        // Services
        var timeitCallbacks = new TimeItCallbacks();
        var callbacksTriggers = timeitCallbacks.GetTriggers();
        var services = GetServices(config);
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
    
    private static List<IAssertor> GetAssertors(Config configuration)
    {
        var assertorsList = new List<IAssertor>();
        if (configuration.Assertors is null || configuration.Assertors.Count == 0)
        {
            assertorsList.Add(new DefaultAssertor());
        }
        else
        {
            foreach (var assertor in configuration.Assertors)
            {
                if (assertor is null)
                {
                    continue;
                }

                var asmLoadContext = AssemblyLoadContext.Default;
                if (!string.IsNullOrEmpty(assertor.FilePath))
                {
                    var assembly = asmLoadContext.LoadFromAssemblyPath(assertor.FilePath);
                    if (!string.IsNullOrEmpty(assertor.Type))
                    {
                        if (assembly.GetType(assertor.Type, throwOnError: true) is { } type)
                        {
                            if (Activator.CreateInstance(type) is IAssertor instance)
                            {
                                assertorsList.Add(instance);
                            }
                            else
                            {
                                AnsiConsole.MarkupLine("[red]Error creating assertor[/]: {0}",
                                    type.FullName ?? string.Empty);
                            }
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(assertor.Name))
                {
                    foreach (var assembly in asmLoadContext.Assemblies)
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

                                if (iface.FullName == typeof(IAssertor).FullName)
                                {
                                    if (Activator.CreateInstance(typeInfo) is IAssertor instance &&
                                        instance.Name == assertor.Name)
                                    {
                                        assertorsList.Add(instance);
                                    }
                                    else
                                    {
                                        AnsiConsole.MarkupLine("[red]Error creating assertor[/]: {0} | {1}", assertor.Name,
                                            typeInfo.FullName ?? string.Empty);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        return assertorsList;
    }

    private static List<IService> GetServices(Config configuration)
    {
        var servicesList = new List<IService>();
        if (configuration.Services is null || configuration.Services.Count == 0)
        {
            return servicesList;
        }

        foreach (var service in configuration.Services)
        {
            if (service is null)
            {
                continue;
            }

            var asmLoadContext = AssemblyLoadContext.Default;
            if (!string.IsNullOrEmpty(service.FilePath))
            {
                var assembly = asmLoadContext.LoadFromAssemblyPath(service.FilePath);
                if (!string.IsNullOrEmpty(service.Type))
                {
                    if (assembly.GetType(service.Type, throwOnError: true) is { } type)
                    {
                        if (Activator.CreateInstance(type) is IService instance)
                        {
                            servicesList.Add(instance);
                        }
                        else
                        {
                            AnsiConsole.MarkupLine("[red]Error creating service[/]: {0}",
                                type.FullName ?? string.Empty);
                        }
                    }
                }
            }
            else if (!string.IsNullOrEmpty(service.Name))
            {
                foreach (var assembly in asmLoadContext.Assemblies)
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

                            if (iface.FullName == typeof(IService).FullName)
                            {
                                if (Activator.CreateInstance(typeInfo) is IService instance &&
                                    instance.Name == service.Name)
                                {
                                    servicesList.Add(instance);
                                }
                                else
                                {
                                    AnsiConsole.MarkupLine("[red]Error creating service[/]: {0} | {1}", service.Name,
                                        typeInfo.FullName ?? string.Empty);
                                }
                            }
                        }
                    }
                }
            }
        }

        return servicesList;
    }
}
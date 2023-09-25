using Spectre.Console;
using TimeIt;
using TimeIt.Common.Configuration;
using TimeIt.Common.Exporters;
using TimeIt.Common.Results;
using TimeIt.DatadogExporter;
using System.CommandLine;
using System.Reflection;
using System.Runtime.Loader;
using TimeIt.Common.Assertors;
using Status = TimeIt.Common.Results.Status;

AnsiConsole.MarkupLine("[bold dodgerblue1 underline]TimeIt v{0}[/]", GetVersion());

var argument = new Argument<string>("configuration file", "The JSON configuration file");
var templateVariables = new Option<TemplateVariables>("--variable", isDefault: true, description: "Variables used to instantiate the configuration file",
    parseArgument: result =>
    {
        var tvs = new TemplateVariables();

        foreach (var token in result.Tokens)
        {
            var variableValue = token.Value; ;
            var idx = variableValue.IndexOf('=');
            if (idx == -1)
            {
                AnsiConsole.MarkupLine("[bold red]Unknown format: variable must be of the form[/][bold blue] key=value[/]");
                continue;
            }

            if (idx == variableValue.Length - 1)
            {
                AnsiConsole.MarkupLine("[bold red]No variable value provided. Skipped.[/]");
                continue;
            }

            if (idx == 0)
            {
                AnsiConsole.MarkupLine("[bold red]No variable name provided. Skipped.[/]");
                continue;
            }

            var keyVal = variableValue.Split('=');
            tvs.Add(keyVal[0], keyVal[1]);
        }
        return tvs;

    }) { Arity = ArgumentArity.OneOrMore };

var root = new RootCommand
{
    argument,
    templateVariables
};

root.SetHandler(async (configFile, templateVariables) =>
{
    // Load configuration
    var config = Config.LoadConfiguration(configFile);
    config.JsonExporterFilePath = templateVariables.Expand(config.JsonExporterFilePath);

    // Enable exporters
    var exporters = new List<IExporter>();
    exporters.Add(new ConsoleExporter());
    exporters.Add(new JsonExporter());
    exporters.Add(new TimeItDatadogExporter());
    
    // Assertors
    var assertors = new List<IAssertor>();
    if (config.Assertors is null || config.Assertors.Count == 0)
    {
        assertors.Add(new DefaultAssertor());
    }
    else
    {
        foreach (var assertor in config.Assertors)
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
                            assertors.Add(instance);
                        }
                        else
                        {
                            AnsiConsole.MarkupLine("[red]Error creating assertor[/]: {0}", type.FullName ?? string.Empty);
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
                                if (Activator.CreateInstance(typeInfo) is IAssertor instance && instance.Name == assertor.Name)
                                {
                                    assertors.Add(instance);
                                }
                                else
                                {
                                    AnsiConsole.MarkupLine("[red]Error creating assertor[/]: {0} | {1}", assertor.Name, typeInfo.FullName ?? string.Empty);
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    foreach (var assertor in assertors)
    {
        assertor.SetConfiguration(config);
    }
    
    // Create scenario processor
    var processor = new ScenarioProcessor(config, templateVariables, assertors);

    AnsiConsole.MarkupLine("[bold aqua]Warmup count:[/] {0}", config.WarmUpCount);
    AnsiConsole.MarkupLine("[bold aqua]Count:[/] {0}", config.Count);
    AnsiConsole.MarkupLine("[bold aqua]Number of Scenarios:[/] {0}", config.Scenarios.Count);
    AnsiConsole.MarkupLine("[bold aqua]Exporters:[/] {0}", string.Join(", ", exporters.Select(e => e.Name)));
    AnsiConsole.MarkupLine("[bold aqua]Assertors:[/] {0}", string.Join(", ", assertors.Select(e => e.Name)));
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

        if (scenarioWithErrors > 0)
        {
            Environment.Exit(1);
        }
    }
}, argument, templateVariables);


await root.InvokeAsync(args);

return;

static string GetVersion()
{
    var version = typeof(Utils).Assembly.GetName().Version;
    if (version is null)
    {
        return "unknown";
    }

    return $"{version.Major}.{version.Minor}.{version.Build}";
}

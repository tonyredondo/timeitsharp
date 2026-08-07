using System.Diagnostics.CodeAnalysis;
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
        ArgumentNullException.ThrowIfNull(configBuilder);
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
        // Snapshot the host at RunAsync entry, before extension constructors/Initialize callbacks
        // can mutate process-wide state. Each child receives a private copy of this snapshot.
        var environmentVariables = ScenarioProcessor.CaptureEnvironmentVariables();
        // Validate before cloning: Clone assumes all collections and nested process data are
        // present, and malformed input should result in a useful configuration error.
        config.Validate();
        config = config.Clone();

        options ??= new TimeItOptions(new TemplateVariables());
        cancellationToken ??= CancellationToken.None;
        var templateVariables = options.TemplateVariables ?? new TemplateVariables();
        var knownSecretValues = Utils.GetSensitiveEnvironmentValues(config.EnvironmentVariables)
            .Concat(Utils.GetSensitiveEnvironmentSnapshotValues(environmentVariables))
            .Concat(Utils.GetTemplateSecretValues(templateVariables))
            .Concat(config.PathValidations.Take(Utils.MaxTagEntries))
            .Concat(new[] { config.FilePath, config.Path, config.FileName, config.JsonExporterFilePath }
                .OfType<string>())
            .Concat(Utils.GetPathRedactionValues(new[] { config.ProcessName, config.WorkingDirectory }
                .Concat(config.Scenarios.SelectMany(s => new[] { s.ProcessName, s.WorkingDirectory }))))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var statesByType = options.StatesByType;
        var timeitCallbacks = new TimeItCallbacks(cancellationToken.Value);
        var callbacksTriggers = timeitCallbacks.GetTriggers();
        var callbacksInitialized = true;
        var scenariosResults = new List<ScenarioResult>();
        var scenarioWithErrors = 0;
        var exporterErrors = 0;
        var initializedExporters = new HashSet<IExporter>(ReferenceEqualityComparer.Instance);
        var lifecycleErrors = false;
        var beforeAllAttempted = false;
        var beforeAllCompleted = false;
        var afterAllAttempted = false;
        var scenariosCleaned = false;
        ScenarioProcessor? processor = null;
        var engineExitCode = 1;

        try
        {
            // Prepare configuration before extensions are initialized.
            config.JsonExporterFilePath = templateVariables.Expand(config.JsonExporterFilePath);
            ExpandAssemblyLoadInfoValues(config.Exporters, templateVariables);
            ExpandAssemblyLoadInfoValues(config.Assertors, templateVariables);
            ExpandAssemblyLoadInfoValues(config.Services, templateVariables);

            // Keep extension loading and initialization inside the lifecycle guard. A custom
            // service can subscribe callbacks and then fail during initialization; those
            // callbacks still get a chance to finish in the finally block.
            var exportersInfo = GetFromAssemblyLoadInfoList(
                config.Exporters,
                () => new List<IExporter> { new ConsoleExporter(), new JsonExporter(), new DatadogExporter() },
                config.Path);
            var exporters = exportersInfo.Select(i => i.Instance).ToList();
            // Resolve first, then enable the private configuration clone based on the actual
            // instance. This covers aliases, in-memory registrations, and FilePath+Type selectors
            // without mutating a caller-owned Config or trusting a spoofed type string.
            if (exportersInfo.Any(item => item.LoadInfo is not null && item.Instance is DatadogExporter))
            {
                config.EnableDatadog = true;
            }

            var assertorsInfo = GetFromAssemblyLoadInfoList(
                config.Assertors,
                () => new List<IAssertor> { new DefaultAssertor() },
                config.Path);
            foreach (var assertor in assertorsInfo)
            {
                var state = statesByType.GetValueOrDefault(assertor.Instance.GetType());
                assertor.Instance.Initialize(new InitOptions(config, assertor.LoadInfo, templateVariables, state) { HostEnvironment = environmentVariables });
            }

            var assertors = assertorsInfo
                .Where(i => i.Instance.Enabled)
                .Select(i => i.Instance)
                .ToList();

            var servicesInfo = GetFromAssemblyLoadInfoList<IService>(
                config.Services,
                () => new List<IService> { new NoopService() },
                config.Path);
            var services = servicesInfo.Select(i => i.Instance).ToList();
            foreach (var service in servicesInfo)
            {
                var state = statesByType.GetValueOrDefault(service.Instance.GetType());
                service.Instance.Initialize(new InitOptions(config, service.LoadInfo, templateVariables, state) { HostEnvironment = environmentVariables }, timeitCallbacks);
            }

            // Initialize exporters before any scenario lifecycle callback. Datadog creates its
            // session/module here so the benchmark spans contain the target processes; export
            // later reuses the same initialized instances and never initializes twice.
            foreach (var exporterInfo in exportersInfo)
            {
                try
                {
                    var state = statesByType.GetValueOrDefault(exporterInfo.Instance.GetType());
                    exporterInfo.Instance.Initialize(new InitOptions(config, exporterInfo.LoadInfo, templateVariables, state) { HostEnvironment = environmentVariables });
                    initializedExporters.Add(exporterInfo.Instance);
                }
                catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException)
                {
                    exporterErrors++;
                    AnsiConsole.MarkupLine(
                        "[red]Error initializing exporter '{0}':[/]",
                        Utils.EscapeMarkup(Utils.SanitizeText(exporterInfo.Instance.Name, knownSecretValues)));
                    AnsiConsole.WriteException(Utils.SanitizeException(ex, knownSecretValues));
                    if (exporterInfo.Instance is IDisposable disposable)
                    {
                        try
                        {
                            disposable.Dispose();
                        }
                        catch (Exception disposeError) when (disposeError is not OutOfMemoryException &&
                                                             disposeError is not StackOverflowException)
                        {
                            AnsiConsole.WriteException(Utils.SanitizeException(disposeError, knownSecretValues));
                        }
                    }
                }
            }

            processor = new ScenarioProcessor(
                config,
                templateVariables,
                assertors,
                services,
                callbacksTriggers,
                environmentVariables);

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
            AnsiConsole.MarkupLine(
                "[bold aqua]Exporters:[/] {0}",
                Utils.EscapeMarkup(string.Join(", ", exporters.Select(e =>
                    Utils.SanitizeText(e.Name, knownSecretValues)))));
            AnsiConsole.MarkupLine(
                "[bold aqua]Assertors:[/] {0}",
                Utils.EscapeMarkup(string.Join(", ", assertors.Select(e =>
                    Utils.SanitizeText(e.Name, knownSecretValues)))));
            AnsiConsole.MarkupLine(
                "[bold aqua]Services:[/] {0}",
                Utils.EscapeMarkup(string.Join(", ", services.Select(e =>
                    Utils.SanitizeText(e.Name, knownSecretValues)))));
            AnsiConsole.WriteLine();

            if (config is { Count: > 0, Scenarios.Count: > 0 } &&
                !cancellationToken.Value.IsCancellationRequested)
            {
                if (config.Scenarios.Any(s => s.IsBaseline))
                {
                    config.Scenarios = config.Scenarios.OrderByDescending(s => s.IsBaseline).ToList();
                }

                try
                {
                    beforeAllAttempted = true;
                    callbacksTriggers.BeforeAllScenariosStarts(config.Scenarios);
                    beforeAllCompleted = true;
                }
                catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException)
                {
                    lifecycleErrors = true;
                    AnsiConsole.MarkupLine("[red]Error running BeforeAllScenariosStarts:[/]");
                    AnsiConsole.WriteException(Utils.SanitizeException(ex, knownSecretValues));
                }

                if (beforeAllCompleted)
                {
                    for (var i = 0; i < config.Scenarios.Count; i++)
                    {
                        if (cancellationToken.Value.IsCancellationRequested)
                        {
                            scenarioWithErrors++;
                            break;
                        }

                        var scenario = config.Scenarios[i];
                        ScenarioResult? result = null;
                        try
                        {
                            processor.PrepareScenario(scenario);
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                            result = await processor.ProcessScenarioAsync(
                                i,
                                scenario,
                                cancellationToken: cancellationToken.Value).ConfigureAwait(false);
                        }
                        catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException)
                        {
                            lifecycleErrors = true;
                            scenarioWithErrors++;
                            AnsiConsole.MarkupLine(
                                "[red]Error processing scenario '{0}':[/]",
                                Utils.EscapeMarkup(Utils.SanitizeText(scenario.Name, knownSecretValues)));
                            AnsiConsole.WriteException(Utils.SanitizeException(ex, knownSecretValues));
                        }

                        // Add the result before checking cancellation. A command may have
                        // completed successfully and then requested cancellation from End.
                        if (result is not null)
                        {
                            scenariosResults.Add(result);
                            if (result.Status != Status.Passed)
                            {
                                scenarioWithErrors++;
                            }
                        }

                        if (processor.HasLifecycleErrors)
                        {
                            lifecycleErrors = true;
                        }

                        if (result is null)
                        {
                            scenarioWithErrors++;
                            break;
                        }

                        if (cancellationToken.Value.IsCancellationRequested)
                        {
                            break;
                        }
                    }
                }

                CleanScenarios();
                if (beforeAllAttempted)
                {
                    RunAfterAll();
                }

                var results = new TimeitResult
                {
                    Scenarios = scenariosResults,
                    Overheads = Utils.GetComparisonTableData(scenariosResults),
                };

                foreach (var exporter in exportersInfo)
                {
                    if (!initializedExporters.Contains(exporter.Instance))
                    {
                        continue;
                    }

                    try
                    {
                        if (exporter.Instance.Enabled)
                        {
                            exporter.Instance.Export(results);
                        }
                    }
                    catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException)
                    {
                        exporterErrors++;
                        AnsiConsole.MarkupLine(
                            "[red]Error running exporter '{0}':[/]",
                            Utils.EscapeMarkup(Utils.SanitizeText(exporter.Instance.Name, knownSecretValues)));
                        AnsiConsole.WriteException(Utils.SanitizeException(ex, knownSecretValues));
                    }
                }
            }

            engineExitCode = cancellationToken.Value.IsCancellationRequested ||
                scenarioWithErrors > 0 || exporterErrors > 0 || lifecycleErrors ? 1 : 0;
        }
        finally
        {
            // If an unexpected exception interrupted the scenario loop, preserve the same order
            // as the successful path: ScenarioFinish (inside processor), CleanScenario, AfterAll,
            // then OnFinish. Each phase is attempted at most once.
            CleanScenarios();
            if (beforeAllAttempted)
            {
                RunAfterAll();
            }

            if (callbacksInitialized)
            {
                try
                {
                    callbacksTriggers.Finish();
                }
                catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException)
                {
                    lifecycleErrors = true;
                    AnsiConsole.MarkupLine("[red]Error running OnFinish:[/]");
                    AnsiConsole.WriteException(Utils.SanitizeException(ex, knownSecretValues));
                }
            }

            // Finish callbacks may legitimately inspect or recreate metadata. Release only after
            // they have completed so no static entry survives the run.
            foreach (var scenario in config.Scenarios)
            {
                DatadogMetadata.Release(scenario);
            }

            foreach (var result in scenariosResults)
            {
                if (result.Scenario is { } scenario)
                {
                    DatadogMetadata.Release(scenario);
                }
            }

            NotifyRunOutcome();
            DisposeInitializedExporters();
        }

        return cancellationToken.Value.IsCancellationRequested || lifecycleErrors ? 1 : engineExitCode;

        void NotifyRunOutcome()
        {
            var succeeded = engineExitCode == 0 &&
                            !cancellationToken.Value.IsCancellationRequested &&
                            !lifecycleErrors &&
                            scenarioWithErrors == 0 &&
                            exporterErrors == 0;
            foreach (var exporter in initializedExporters)
            {
                if (exporter is not IRunOutcomeAwareExporter outcomeAwareExporter)
                {
                    continue;
                }

                try
                {
                    outcomeAwareExporter.SetRunOutcome(succeeded);
                }
                catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException)
                {
                    lifecycleErrors = true;
                    exporterErrors++;
                    AnsiConsole.MarkupLine("[red]Error finalizing exporter '{0}':[/]",
                        Utils.EscapeMarkup(Utils.SanitizeText(exporter.Name, knownSecretValues)));
                    AnsiConsole.WriteException(Utils.SanitizeException(ex, knownSecretValues));
                }
            }
        }

        void DisposeInitializedExporters()
        {
            foreach (var exporter in initializedExporters.Reverse())
            {
                if (exporter is not IDisposable disposable)
                {
                    continue;
                }

                try
                {
                    disposable.Dispose();
                }
                catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException)
                {
                    lifecycleErrors = true;
                    exporterErrors++;
                    AnsiConsole.MarkupLine("[red]Error disposing exporter '{0}':[/]",
                        Utils.EscapeMarkup(Utils.SanitizeText(exporter.Name, knownSecretValues)));
                    AnsiConsole.WriteException(Utils.SanitizeException(ex, knownSecretValues));
                }
            }
        }

        void CleanScenarios()
        {
            if (scenariosCleaned || processor is null)
            {
                return;
            }

            scenariosCleaned = true;
            foreach (var scenario in config.Scenarios)
            {
                try
                {
                    processor.CleanScenario(scenario);
                }
                catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException)
                {
                    lifecycleErrors = true;
                    AnsiConsole.MarkupLine("[red]Error cleaning scenario:[/]");
                    AnsiConsole.WriteException(Utils.SanitizeException(ex, knownSecretValues));
                }
            }
        }

        void RunAfterAll()
        {
            if (afterAllAttempted)
            {
                return;
            }

            afterAllAttempted = true;
            try
            {
                callbacksTriggers.AfterAllScenariosFinishes(scenariosResults);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException)
            {
                lifecycleErrors = true;
                AnsiConsole.MarkupLine("[red]Error running AfterAllScenariosFinishes:[/]");
                AnsiConsole.WriteException(Utils.SanitizeException(ex, knownSecretValues));
            }
        }
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

    [RequiresUnreferencedCode("Loads configured extensions by name or assembly path.")]
    private static List<(T Instance, AssemblyLoadInfo? LoadInfo)> GetFromAssemblyLoadInfoList<T>(
        IReadOnlyList<AssemblyLoadInfo> assemblyLoadInfos,
        Func<List<T>>? defaultListFunc = null,
        string? baseDirectory = null)
        where T : class, INamedExtension
    {
        return ExtensionResolver.Resolve(assemblyLoadInfos, defaultListFunc, baseDirectory);
    }
}

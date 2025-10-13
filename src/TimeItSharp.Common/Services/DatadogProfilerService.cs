using CliWrap;
using DatadogTestLogger.Vendors.Datadog.Trace;
using DatadogTestLogger.Vendors.Datadog.Trace.Ci;
using DatadogTestLogger.Vendors.Datadog.Trace.Ci.Tags;
using DatadogTestLogger.Vendors.Datadog.Trace.Configuration;
using DatadogTestLogger.Vendors.Datadog.Trace.Util;
using Spectre.Console;
using TimeItSharp.Common.Configuration;
using TimeItSharp.Common.Results;

namespace TimeItSharp.Common.Services;

public sealed class DatadogProfilerService : IService
{
    private bool _isEnabled;
    private IReadOnlyDictionary<string, string?>? _profilerEnvironmentVariables = null;
    private DatadogProfilerConfiguration? _profilerConfiguration = null;
    private Config? _configuration = null;

    public string Name => "DatadogProfiler";

    public void Initialize(InitOptions options, TimeItCallbacks callbacks)
    {
        if (options.State is DatadogProfilerConfiguration profilerConfiguration)
        {
            _profilerConfiguration = profilerConfiguration;
        }
        else
        {
            _profilerConfiguration = new(options.LoadInfo?.Options);
        }

        _configuration = options.Configuration;
        _profilerEnvironmentVariables = GetProfilerEnvironmentVariables();
        callbacks.OnScenarioStart += CallbacksOnOnScenarioStart;
        callbacks.OnExecutionStart += CallbacksOnOnExecutionStart;
        callbacks.OnFinish += CallbacksOnOnFinish;
    }

    private void CallbacksOnOnFinish()
    {
        AnsiConsole.MarkupLine(_isEnabled
            ? $"[lime]The Datadog profiler was successfully attached to the .NET processes.[/]"
            : "[red]The Datadog profiler could not be attached to the .NET processes.[/]");
    }

    private void CallbacksOnOnScenarioStart(TimeItCallbacks.ScenarioStartArg scenario)
    {
        if (_profilerEnvironmentVariables is not null && _profilerConfiguration?.UseExtraRun == true)
        {
            var enabledScenarios = _profilerConfiguration.EnabledScenarios;
            if (enabledScenarios is null ||
                (enabledScenarios.TryGetValue(scenario.Scenario.Name, out var isEnabled) && isEnabled))
            {
                var count = _profilerConfiguration.ExtraRunCount;
                if (count < 1)
                {
                    count = Math.Max((_configuration?.Count ?? 1) / 10, 1);
                }

                scenario.RepeatScenarioForService(this, count);
            }
        }
    }

    private void CallbacksOnOnExecutionStart(DataPoint datapoint, TimeItPhase phase, ref Command command)
    {
        if (_profilerEnvironmentVariables is { } profilerEnvironmentVariables &&
            datapoint.Scenario is { } scenario)
        {
            var enabledScenarios = _profilerConfiguration?.EnabledScenarios;
            if (enabledScenarios is null ||
                (enabledScenarios.TryGetValue(scenario.Name, out var isEnabled) && isEnabled))
            {
                var runProfiler = _profilerConfiguration?.UseExtraRun == true &&
                                  phase == TimeItPhase.ExtraRun;

                runProfiler = runProfiler ||
                              (_profilerConfiguration?.UseExtraRun != true &&
                               phase == TimeItPhase.Run);

                if (runProfiler)
                {
                    var envVar = new Dictionary<string, string?>(profilerEnvironmentVariables);
                    foreach (var kvp in command.EnvironmentVariables)
                    {
                        envVar[kvp.Key] = kvp.Value;
                    }

                    DatadogMetadata.GetIds(scenario, out var traceId, out var spanId);
                    envVar["DD_INTERNAL_CIVISIBILITY_SPANID"] = spanId.ToString();

                    command = command.WithEnvironmentVariables(envVar);
                    _isEnabled = true;
                }
            }
        }
    }

    public object? GetExecutionServiceData() => null;

    public object? GetScenarioServiceData() => null;

    private static Dictionary<string, string?>? GetProfilerEnvironmentVariables()
    {
        string? monitoringHome = null;
        string? profiler32Path = null;
        string? profiler64Path = null;
        string? loaderConfig = null;
        string? ldPreload = null;
        try
        {
            foreach (var homePath in GetProfilersHomeFolder())
            {
                if (string.IsNullOrEmpty(homePath) || !Directory.Exists(homePath))
                {
                    continue;
                }

                var tmpHomePath = Path.GetFullPath(homePath);
                if (GetProfilerPaths(tmpHomePath, ref profiler32Path, ref profiler64Path, ref loaderConfig, ref ldPreload))
                {
                    monitoringHome = tmpHomePath;
                    break;
                }
            }
        }
        catch (PlatformNotSupportedException)
        {
            // Store the exception if the platform is not supported and ignore everything.
            return null;
        }

        if (string.IsNullOrEmpty(monitoringHome))
        {
            return null;
        }

        var tracer = Tracer.Instance;
        var environment = new Dictionary<string, string?>();
        if (!environment.TryGetValue(ConfigurationKeys.ServiceName, out _))
        {
            environment[ConfigurationKeys.ServiceName] = tracer.DefaultServiceName;
        }

        if (!environment.TryGetValue(ConfigurationKeys.Environment, out _) &&
            tracer.Settings.EnvironmentInternal is { } environmentInternal)
        {
            environment[ConfigurationKeys.Environment] = environmentInternal;
        }

        if (!environment.TryGetValue(ConfigurationKeys.ServiceVersion, out _) &&
            tracer.Settings.ServiceVersionInternal is { } serviceVersionInternal)
        {
            environment[ConfigurationKeys.ServiceVersion] = serviceVersionInternal;
        }

        const string ProfilerId = "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}";
        environment["COR_ENABLE_PROFILING"] = "1";
        environment["CORECLR_ENABLE_PROFILING"] = "1";
        environment["COR_PROFILER"] = ProfilerId;
        environment["CORECLR_PROFILER"] = ProfilerId;
        environment["DD_DOTNET_TRACER_HOME"] = monitoringHome;

        if (profiler32Path != null)
        {
            environment["COR_PROFILER_PATH_32"] = profiler32Path;
            environment["CORECLR_PROFILER_PATH_32"] = profiler32Path;
        }

        environment["COR_PROFILER_PATH_64"] = profiler64Path;
        environment["CORECLR_PROFILER_PATH_64"] = profiler64Path;

        if (ldPreload != null)
        {
            environment["LD_PRELOAD"] = ldPreload;
        }

        environment["DD_NATIVELOADER_CONFIGFILE"] = loaderConfig;

        // CI Visibility integration environment variables
        environment[ConfigurationKeys.CIVisibility.Enabled] = "1";
        environment["DD_INTERNAL_CIVISIBILITY_RUNTIMEID"] = RuntimeId.Get();

        // Profiler options
        const string profilerEnabled = "DD_PROFILING_ENABLED";
        if (!environment.TryGetValue(profilerEnabled, out _))
        {
            environment[profilerEnabled] = "1";
        }

        const string profilerCPUEnabled = "DD_PROFILING_CPU_ENABLED";
        if (!environment.TryGetValue(profilerCPUEnabled, out _))
        {
            environment[profilerCPUEnabled] = "1";
        }

        const string profilerWalltimeEnabled = "DD_PROFILING_WALLTIME_ENABLED";
        if (!environment.TryGetValue(profilerWalltimeEnabled, out _))
        {
            environment[profilerWalltimeEnabled] = "1";
        }

        const string profilerExceptionEnabled = "DD_PROFILING_EXCEPTION_ENABLED";
        if (!environment.TryGetValue(profilerExceptionEnabled, out _))
        {
            environment[profilerExceptionEnabled] = "1";
        }

        const string profilerAllocationEnabled = "DD_PROFILING_ALLOCATION_ENABLED";
        if (!environment.TryGetValue(profilerAllocationEnabled, out _))
        {
            environment[profilerAllocationEnabled] = "1";
        }

        const string profilerLockEnabled = "DD_PROFILING_LOCK_ENABLED";
        if (!environment.TryGetValue(profilerLockEnabled, out _))
        {
            environment[profilerLockEnabled] = "1";
        }

        const string profilerGcEnabled = "DD_PROFILING_GC_ENABLED";
        if (!environment.TryGetValue(profilerGcEnabled, out _))
        {
            environment[profilerGcEnabled] = "1";
        }

        const string profilerHeapEnabled = "DD_PROFILING_HEAP_ENABLED";
        if (!environment.TryGetValue(profilerHeapEnabled, out _))
        {
            environment[profilerHeapEnabled] = "1";
        }

        environment["DD_PROFILING_AGENTLESS"] = CIVisibility.Settings.Agentless ? "1" : "0";
        environment["DD_PROFILING_UPLOAD_PERIOD"] = "90";
        environment["DD_INTERNAL_PROFILING_SAMPLING_RATE"] = "1";
        environment["DD_INTERNAL_PROFILING_WALLTIME_THREADS_THRESHOLD"] = "64";
        environment["DD_INTERNAL_PROFILING_CODEHOTSPOTS_THREADS_THRESHOLD"] = "64";
        environment["DD_INTERNAL_PROFILING_CPUTIME_THREADS_THRESHOLD"] = "128";
        environment["DD_INTERNAL_PROFILING_TIMESTAMPS_AS_LABEL_ENABLED "] = "1";
        environment["DD_PROFILING_FRAMES_NATIVE_ENABLED "] = "1";

        // Tags
        var tagsList = new List<string>();

        // Git data
        if (CIEnvironmentValues.Instance is { } ciEnv)
        {
            environment["DD_GIT_REPOSITORY_URL"] = ciEnv.Repository;
            environment["DD_GIT_COMMIT_SHA"] = ciEnv.Commit;

            if (!string.IsNullOrEmpty(ciEnv.Branch))
            {
                tagsList.Add($"{CommonTags.GitBranch}:{ciEnv.Branch}");
            }

            if (!string.IsNullOrEmpty(ciEnv.Tag))
            {
                tagsList.Add($"{CommonTags.GitTag}:{ciEnv.Tag}");
            }
        }

        var newDdTags = string.Join(", ", tagsList);
        if (environment.TryGetValue("DD_TAGS", out var ddTags))
        {
            environment["DD_TAGS"] = newDdTags + "," + ddTags;
        }
        else
        {
            environment["DD_TAGS"] = newDdTags;
        }

        return environment;

        static IEnumerable<string> GetProfilersHomeFolder()
        {
            // try to locate it from the environment variable
            yield return EnvironmentHelpers.GetEnvironmentVariable("DD_DOTNET_TRACER_HOME");

#if NATIVE_AOT
            // try to locate it in the default path using relative path from the benchmark assembly.
            yield return Path.Combine(
                System.AppContext.BaseDirectory,
                "datadog");
#else
            // try to locate it in the default path using relative path from the benchmark assembly.
            yield return Path.Combine(
                Path.GetDirectoryName(typeof(Datadog.Trace.BenchmarkDotNet.DatadogDiagnoser).Assembly.Location) ?? string.Empty,
                "datadog");
#endif

            // try to locate it in the default path using relative path from the benchmark assembly.
            yield return Path.Combine(
                Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory) ?? string.Empty,
                "datadog");
        
            // try to locate it in the default path using relative path from the current directory.
            yield return Path.Combine(
                Environment.CurrentDirectory,
                "datadog");
        }
        
        static bool GetProfilerPaths(string monitoringHome, ref string? profiler32Path, ref string? profiler64Path, ref string? loaderConfig, ref string? ldPreload)
        {
            var osPlatform = FrameworkDescription.Instance.OSPlatform;
            var processArch = FrameworkDescription.Instance.ProcessArchitecture;
            if (string.Equals(osPlatform, OSPlatformName.Windows, StringComparison.OrdinalIgnoreCase))
            {
                // set required file paths
                profiler32Path = Path.Combine(monitoringHome, "win-x86", "Datadog.Trace.ClrProfiler.Native.dll");
                profiler64Path = Path.Combine(monitoringHome, "win-x64", "Datadog.Trace.ClrProfiler.Native.dll");
                loaderConfig = Path.Combine(monitoringHome, "win-x64", "loader.conf");
                ldPreload = null;
            }
            else if (string.Equals(osPlatform, OSPlatformName.Linux, StringComparison.OrdinalIgnoreCase))
            {
                // set required file paths
                if (string.Equals(processArch, "arm64", StringComparison.OrdinalIgnoreCase))
                {
                    const string rid = "linux-arm64";
                    profiler32Path = null;
                    profiler64Path = Path.Combine(monitoringHome, rid, "Datadog.Trace.ClrProfiler.Native.so");
                    loaderConfig = Path.Combine(monitoringHome, rid, "loader.conf");
                    ldPreload = Path.Combine(monitoringHome, rid, "Datadog.Linux.ApiWrapper.x64.so");
                }
                else
                {
                    const string rid = "linux-x64";
                    profiler32Path = null;
                    profiler64Path = Path.Combine(monitoringHome, rid, "Datadog.Trace.ClrProfiler.Native.so");
                    loaderConfig = Path.Combine(monitoringHome, rid, "loader.conf");
                    ldPreload = Path.Combine(monitoringHome, rid, "Datadog.Linux.ApiWrapper.x64.so");
                }
            }
            else if (string.Equals(osPlatform, OSPlatformName.MacOS, StringComparison.OrdinalIgnoreCase))
            {
                throw new PlatformNotSupportedException("Datadog Profiler is not supported in macOS");
            }

            if (!File.Exists(profiler64Path) ||
                (profiler32Path is not null && !File.Exists(profiler64Path)) ||
                !File.Exists(loaderConfig))
            {
                return false;
            }

            return true;
        }
    }
}
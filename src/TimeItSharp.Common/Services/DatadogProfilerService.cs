using System.Runtime.InteropServices;
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
    private const string DatadogProfilerPackageVersion = "2.61.0";
    private const string TracerProfilerId = "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}";
    private const string NativeProfilerId = "{BD1A650D-AC5D-4896-B64F-D6FA25D6B26A}";

    private bool _environmentConfigured;
    private IReadOnlyDictionary<string, string?>? _profilerEnvironmentVariables;
    private DatadogProfilerConfiguration? _profilerConfiguration;
    private Config? _configuration;
    private string? _profilerDiagnostic;

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
        _profilerEnvironmentVariables = GetProfilerEnvironmentVariables(out _profilerDiagnostic);
        callbacks.OnScenarioStart += CallbacksOnOnScenarioStart;
        callbacks.OnExecutionStart += CallbacksOnOnExecutionStart;
        callbacks.OnFinish += CallbacksOnOnFinish;
    }

    private void CallbacksOnOnFinish()
    {
        if (_environmentConfigured)
        {
            // Environment injection is observable here; whether CoreCLR actually loads the
            // profiler can only be reported by the target process. Do not claim an attach based
            // solely on constructing a StartInfo environment dictionary.
            AnsiConsole.MarkupLine(
                "[lime]Datadog profiler environment configured for the .NET processes (runtime attach is not verified).[/]");
        }
        else if (_profilerEnvironmentVariables is null)
        {
            AnsiConsole.MarkupLine("[yellow]Datadog profiler was not configured.[/]");
            if (!string.IsNullOrWhiteSpace(_profilerDiagnostic))
            {
                AnsiConsole.WriteLine(_profilerDiagnostic);
            }
        }
        else
        {
            AnsiConsole.MarkupLine(
                "[yellow]Datadog profiler assets were found, but no benchmark process received the profiler environment.[/]");
        }
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
        if (datapoint.Scenario is not { } scenario)
        {
            return;
        }

        var enabledScenarios = _profilerConfiguration?.EnabledScenarios;
        var scenarioEnabled = enabledScenarios is null ||
                              (enabledScenarios.TryGetValue(scenario.Name, out var isEnabled) && isEnabled);
        var runProfiler = scenarioEnabled &&
                          ((_profilerConfiguration?.UseExtraRun == true && phase == TimeItPhase.ExtraRun) ||
                           (_profilerConfiguration?.UseExtraRun != true && phase == TimeItPhase.Run));

        if (runProfiler && _profilerEnvironmentVariables is { } profilerEnvironmentVariables)
        {
            // Preserve command-level options, but never allow an inherited v2/v3 profiler home
            // or native path to replace the selected v2.61.0 RID assets.
            var envVar = new Dictionary<string, string?>(profilerEnvironmentVariables);
            foreach (var kvp in command.EnvironmentVariables)
            {
                if (!IsMandatoryProfilerVariable(kvp.Key))
                {
                    envVar[kvp.Key] = kvp.Value;
                }
            }

            DatadogMetadata.GetIds(scenario, out _, out var spanId);
            envVar["DD_INTERNAL_CIVISIBILITY_SPANID"] = spanId.ToString();
            command = command.WithEnvironmentVariables(envVar);
            _environmentConfigured = true;
            return;
        }

        // This service owns profiler selection. Remove inherited profiler variables on warmups,
        // disabled scenarios, and unsupported hosts so a v3 DD_DOTNET_TRACER_HOME cannot be mixed
        // into a v2 run (or attach unexpectedly when v2 assets are unavailable).
        var clearedEnvironment = new Dictionary<string, string?>();
        foreach (var key in command.EnvironmentVariables.Keys)
        {
            if (ProfilerEnvironmentVariableNames.Any(name =>
                    string.Equals(name, key, StringComparison.OrdinalIgnoreCase)))
            {
                clearedEnvironment[key] = null;
            }
        }

        if (clearedEnvironment.Count > 0)
        {
            command = command.WithEnvironmentVariables(clearedEnvironment);
        }
    }


    public object? GetExecutionServiceData() => null;

    public object? GetScenarioServiceData() => null;

    private static readonly string[] ProfilerEnvironmentVariableNames =
    [
        "COR_ENABLE_PROFILING",
        "CORECLR_ENABLE_PROFILING",
        "COR_PROFILER",
        "CORECLR_PROFILER",
        "COR_PROFILER_PATH",
        "CORECLR_PROFILER_PATH",
        "COR_PROFILER_PATH_32",
        "CORECLR_PROFILER_PATH_32",
        "COR_PROFILER_PATH_64",
        "CORECLR_PROFILER_PATH_64",
        "DD_DOTNET_TRACER_HOME",
        "DD_NATIVELOADER_CONFIGFILE",
        ConfigurationKeys.CIVisibility.Enabled,
        "DD_INTERNAL_CIVISIBILITY_RUNTIMEID",
        "DD_INTERNAL_CIVISIBILITY_SPANID",
    ];

    private static bool IsMandatoryProfilerVariable(string key) =>
        key.Equals("COR_ENABLE_PROFILING", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("CORECLR_ENABLE_PROFILING", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("COR_PROFILER", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("CORECLR_PROFILER", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("COR_PROFILER_PATH", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("CORECLR_PROFILER_PATH", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("COR_PROFILER_PATH_32", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("CORECLR_PROFILER_PATH_32", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("COR_PROFILER_PATH_64", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("CORECLR_PROFILER_PATH_64", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("DD_DOTNET_TRACER_HOME", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("DD_NATIVELOADER_CONFIGFILE", StringComparison.OrdinalIgnoreCase) ||
        key.Equals(ConfigurationKeys.CIVisibility.Enabled, StringComparison.OrdinalIgnoreCase) ||
        key.Equals("DD_INTERNAL_CIVISIBILITY_RUNTIMEID", StringComparison.OrdinalIgnoreCase);

    private static Dictionary<string, string?>? GetProfilerEnvironmentVariables(out string? diagnostic)
    {
        diagnostic = null;
        try
        {
            var osPlatform = GetCurrentOsPlatform();
            var processArch = RuntimeInformation.ProcessArchitecture.ToString();
            var isMusl = IsMuslLinux();
            var diagnostics = new List<string>();

            foreach (var homePath in GetProfilersHomeFolder())
            {
                if (string.IsNullOrWhiteSpace(homePath) || !Directory.Exists(homePath))
                {
                    continue;
                }

                var monitoringHome = Path.GetFullPath(homePath);
                if (!TryGetProfilerPaths(
                        monitoringHome,
                        osPlatform,
                        processArch,
                        isMusl,
                        out var profilerPaths,
                        out var pathDiagnostic))
                {
                    if (!string.IsNullOrWhiteSpace(pathDiagnostic))
                    {
                        diagnostics.Add(pathDiagnostic);
                    }

                    continue;
                }

                return BuildProfilerEnvironment(profilerPaths!);
            }

            diagnostic = diagnostics.Count == 0
                ? $"Datadog.Trace.BenchmarkDotNet {DatadogProfilerPackageVersion} profiler assets were not found for RID '{GetRuntimeRid(osPlatform, processArch, isMusl)}'."
                : string.Join(Environment.NewLine, diagnostics.Distinct(StringComparer.Ordinal).Take(3));
            return null;
        }
        catch (PlatformNotSupportedException exception)
        {
            diagnostic = exception.Message;
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            diagnostic = $"Datadog profiler asset discovery failed: {exception.Message}";
            return null;
        }
    }

    private static Dictionary<string, string?> BuildProfilerEnvironment(ProfilerAssetPaths profilerPaths)
    {
        var tracer = Tracer.Instance;
        var environment = new Dictionary<string, string?>
        {
            [ConfigurationKeys.ServiceName] = tracer.DefaultServiceName,
            ["COR_ENABLE_PROFILING"] = "1",
            ["CORECLR_ENABLE_PROFILING"] = "1",
            ["COR_PROFILER"] = TracerProfilerId,
            ["CORECLR_PROFILER"] = TracerProfilerId,
            ["COR_PROFILER_PATH"] = profilerPaths.SelectedTracerPath,
            ["CORECLR_PROFILER_PATH"] = profilerPaths.SelectedTracerPath,
            ["DD_DOTNET_TRACER_HOME"] = profilerPaths.MonitoringHome,
            ["DD_NATIVELOADER_CONFIGFILE"] = profilerPaths.LoaderConfig,
        };

        if (tracer.Settings.EnvironmentInternal is { } environmentInternal)
        {
            environment[ConfigurationKeys.Environment] = environmentInternal;
        }

        if (tracer.Settings.ServiceVersionInternal is { } serviceVersionInternal)
        {
            environment[ConfigurationKeys.ServiceVersion] = serviceVersionInternal;
        }

        if (profilerPaths.Profiler32Path is not null)
        {
            environment["COR_PROFILER_PATH_32"] = profilerPaths.Profiler32Path;
            environment["CORECLR_PROFILER_PATH_32"] = profilerPaths.Profiler32Path;
        }

        if (profilerPaths.Profiler64Path is not null)
        {
            environment["COR_PROFILER_PATH_64"] = profilerPaths.Profiler64Path;
            environment["CORECLR_PROFILER_PATH_64"] = profilerPaths.Profiler64Path;
        }

        // CI Visibility integration environment variables. These values are scoped to the
        // measured child command; the launcher must not mutate the host environment.
        environment[ConfigurationKeys.CIVisibility.Enabled] = "1";
        environment["DD_CIVISIBILITY_LOGS_ENABLED"] = "true";
        environment["DD_INTERNAL_CIVISIBILITY_RUNTIMEID"] = RuntimeId.Get();

        AddDefaultEnvironmentValue(environment, "DD_PROFILING_ENABLED", "1");
        AddDefaultEnvironmentValue(environment, "DD_PROFILING_CPU_ENABLED", "1");
        AddDefaultEnvironmentValue(environment, "DD_PROFILING_WALLTIME_ENABLED", "1");
        AddDefaultEnvironmentValue(environment, "DD_PROFILING_EXCEPTION_ENABLED", "1");
        AddDefaultEnvironmentValue(environment, "DD_PROFILING_ALLOCATION_ENABLED", "1");
        AddDefaultEnvironmentValue(environment, "DD_PROFILING_LOCK_ENABLED", "1");
        AddDefaultEnvironmentValue(environment, "DD_PROFILING_GC_ENABLED", "1");
        AddDefaultEnvironmentValue(environment, "DD_PROFILING_HEAP_ENABLED", "1");
        environment["DD_PROFILING_AGENTLESS"] = CIVisibility.Settings.Agentless ? "1" : "0";
        environment["DD_PROFILING_UPLOAD_PERIOD"] = "90";
        environment["DD_INTERNAL_PROFILING_SAMPLING_RATE"] = "1";
        environment["DD_INTERNAL_PROFILING_WALLTIME_THREADS_THRESHOLD"] = "64";
        environment["DD_INTERNAL_PROFILING_CODEHOTSPOTS_THREADS_THRESHOLD"] = "64";
        environment["DD_INTERNAL_PROFILING_CPUTIME_THREADS_THRESHOLD"] = "128";
        environment["DD_INTERNAL_PROFILING_TIMESTAMPS_AS_LABEL_ENABLED"] = "1";
        environment["DD_PROFILING_FRAMES_NATIVE_ENABLED"] = "1";

        var tagsList = new List<string>();
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
        environment["DD_TAGS"] = environment.TryGetValue("DD_TAGS", out var ddTags)
            ? newDdTags + "," + ddTags
            : newDdTags;
        return environment;

        static void AddDefaultEnvironmentValue(Dictionary<string, string?> environment, string key, string value)
        {
            if (!environment.ContainsKey(key))
            {
                environment[key] = value;
            }
        }
    }

    private static IEnumerable<string?> GetProfilersHomeFolder()
    {
        // Try the explicitly configured home first.
        yield return EnvironmentHelpers.GetEnvironmentVariable("DD_DOTNET_TRACER_HOME");

        // Then locate the content files supplied by Datadog.Trace.BenchmarkDotNet.
        yield return Path.Combine(
            Path.GetDirectoryName(typeof(Datadog.Trace.BenchmarkDotNet.DatadogDiagnoser).Assembly.Location) ?? string.Empty,
            "datadog");
        yield return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "datadog");
        yield return Path.Combine(Environment.CurrentDirectory, "datadog");
    }

    /// <summary>
    /// Resolves the native assets delivered by Datadog.Trace.BenchmarkDotNet 2.61.0.
    /// Only the files in that v2 package are accepted; no assets from another major line are used.
    /// </summary>
    internal static bool TryGetProfilerPaths(
        string monitoringHome,
        string osPlatform,
        string processArch,
        bool isMusl,
        out ProfilerAssetPaths? profilerPaths,
        out string diagnostic)
    {
        profilerPaths = null;
        diagnostic = string.Empty;

        var normalizedArch = NormalizeArchitecture(processArch);
        string rid;
        string loaderRid;
        string selectedTracerPath;
        string selectedProfilerPath;
        string loaderConfig;
        string? profiler32Path = null;
        string? profiler64Path = null;

        if (string.Equals(osPlatform, "Windows", StringComparison.OrdinalIgnoreCase))
        {
            if (normalizedArch is not ("x64" or "x86"))
            {
                diagnostic = CreateUnsupportedArchitectureDiagnostic(osPlatform, processArch, isMusl);
                return false;
            }

            rid = normalizedArch == "x86" ? "win-x86" : "win-x64";
            loaderRid = rid;
            selectedTracerPath = Path.Combine(monitoringHome, rid, "Datadog.Trace.ClrProfiler.Native.dll");
            selectedProfilerPath = Path.Combine(monitoringHome, rid, "Datadog.Profiler.Native.dll");
            loaderConfig = Path.Combine(monitoringHome, rid, "loader.conf");

            // Both Windows assets are part of the v2 package. Keep the optional path when it
            // exists so a 64-bit process can still launch a 32-bit child (and vice versa), but
            // only the current process RID is required for this invocation.
            var win32TracerPath = Path.Combine(monitoringHome, "win-x86", "Datadog.Trace.ClrProfiler.Native.dll");
            var win64TracerPath = Path.Combine(monitoringHome, "win-x64", "Datadog.Trace.ClrProfiler.Native.dll");
            profiler32Path = File.Exists(win32TracerPath) ? win32TracerPath : null;
            profiler64Path = File.Exists(win64TracerPath) ? win64TracerPath : null;
        }
        else if (string.Equals(osPlatform, "Linux", StringComparison.OrdinalIgnoreCase))
        {
            if (normalizedArch == "x64")
            {
                rid = isMusl ? "linux-musl-x64" : "linux-x64";
                // The v2 musl package uses the linux-x64 row in loader.conf.
                loaderRid = "linux-x64";
            }
            else if (normalizedArch == "arm64")
            {
                if (isMusl)
                {
                    diagnostic =
                        "Datadog.Trace.BenchmarkDotNet 2.61.0 does not ship a linux-musl-arm64 profiler asset; " +
                        "the profiler is disabled for this architecture.";
                    return false;
                }

                rid = "linux-arm64";
                loaderRid = rid;
            }
            else
            {
                diagnostic = CreateUnsupportedArchitectureDiagnostic(osPlatform, processArch, isMusl);
                return false;
            }

            selectedTracerPath = Path.Combine(monitoringHome, rid, "Datadog.Trace.ClrProfiler.Native.so");
            selectedProfilerPath = Path.Combine(monitoringHome, rid, "Datadog.Profiler.Native.so");
            loaderConfig = Path.Combine(monitoringHome, rid, "loader.conf");
            profiler64Path = selectedTracerPath;
        }
        else if (string.Equals(osPlatform, "MacOS", StringComparison.OrdinalIgnoreCase))
        {
            diagnostic = "Datadog.Trace.BenchmarkDotNet 2.61.0 provides no macOS native profiler assets.";
            return false;
        }
        else
        {
            diagnostic = $"Datadog profiler is not supported on operating system '{osPlatform}'.";
            return false;
        }

        var missingAssets = new List<string>();
        AddMissingAsset(missingAssets, selectedTracerPath, "tracer profiler");
        AddMissingAsset(missingAssets, selectedProfilerPath, "continuous profiler");
        AddMissingAsset(missingAssets, loaderConfig, "loader.conf");
        if (missingAssets.Count > 0)
        {
            diagnostic =
                $"Datadog.Trace.BenchmarkDotNet {DatadogProfilerPackageVersion} has no complete profiler asset set for RID '{rid}'. " +
                $"Selected architecture: {processArch}; missing {string.Join(", ", missingAssets)}. " +
                "The profiler was not enabled.";
            return false;
        }

        if (!LoaderReferencesProfiler(loaderConfig, loaderRid, selectedProfilerPath, out diagnostic))
        {
            return false;
        }

        profilerPaths = new ProfilerAssetPaths(
            monitoringHome,
            rid,
            selectedTracerPath,
            selectedProfilerPath,
            loaderConfig,
            profiler32Path,
            profiler64Path);
        return true;

        static void AddMissingAsset(List<string> missingAssets, string path, string description)
        {
            if (!File.Exists(path))
            {
                missingAssets.Add($"{description} '{path}'");
            }
        }
    }

    private static bool LoaderReferencesProfiler(
        string loaderConfig,
        string loaderRid,
        string expectedProfilerPath,
        out string diagnostic)
    {
        diagnostic = string.Empty;
        try
        {
            var loaderDirectory = Path.GetDirectoryName(loaderConfig) ?? string.Empty;
            // Datadog's v2 loader.conf is shared-format and lists rows for other
            // architectures too. Only the selected RID row must resolve inside this
            // RID directory; requiring every row would reject the package it ships.
            var expectedPath = Path.GetFullPath(expectedProfilerPath);
            var found = false;
            foreach (var rawLine in File.ReadLines(loaderConfig))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                {
                    continue;
                }

                var fields = line.Split(';');
                if (fields.Length < 4 ||
                    !string.Equals(fields[0], "PROFILER", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(fields[2], loaderRid, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                found = true;
                if (!string.Equals(fields[1], NativeProfilerId, StringComparison.OrdinalIgnoreCase))
                {
                    diagnostic =
                        $"Datadog profiler loader '{loaderConfig}' uses profiler id '{fields[1]}' for RID '{loaderRid}', " +
                        $"not the v2 id '{NativeProfilerId}'.";
                    return false;
                }
                var relativeAsset = fields[3].Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
                var referencedPath = Path.GetFullPath(Path.Combine(loaderDirectory, relativeAsset));
                if (!string.Equals(referencedPath, expectedPath, StringComparison.Ordinal))
                {
                    diagnostic =
                        $"Datadog profiler loader '{loaderConfig}' maps RID '{loaderRid}' to '{fields[3]}', " +
                        $"not the v2 asset '{Path.GetFileName(expectedProfilerPath)}'.";
                    return false;
                }

                if (!File.Exists(referencedPath))
                {
                    diagnostic =
                        $"Datadog profiler loader '{loaderConfig}' references missing asset '{referencedPath}' for RID '{loaderRid}'.";
                    return false;
                }
            }

            if (!found)
            {
                diagnostic =
                    $"Datadog profiler loader '{loaderConfig}' has no PROFILER entry for RID '{loaderRid}'.";
                return false;
            }

            return true;
        }
        catch (IOException exception)
        {
            diagnostic = $"Could not inspect Datadog profiler loader '{loaderConfig}': {exception.Message}";
            return false;
        }
        catch (UnauthorizedAccessException exception)
        {
            diagnostic = $"Could not inspect Datadog profiler loader '{loaderConfig}': {exception.Message}";
            return false;
        }
    }

    private static string GetCurrentOsPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "Windows";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "Linux";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "MacOS";
        }

        return RuntimeInformation.OSDescription;
    }

    private static bool IsMuslLinux()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return false;
        }

        if (RuntimeInformation.RuntimeIdentifier.Contains("musl", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // RuntimeIdentifier is not guaranteed to contain the libc flavor for framework-
        // dependent apps. These are the standard musl dynamic loader locations.
        return File.Exists("/lib/ld-musl-x86_64.so.1") ||
               File.Exists("/usr/lib/ld-musl-x86_64.so.1") ||
               File.Exists("/lib/ld-musl-aarch64.so.1") ||
               File.Exists("/usr/lib/ld-musl-aarch64.so.1");
    }

    private static string NormalizeArchitecture(string processArch) =>
        processArch.Trim().ToLowerInvariant() switch
        {
            "x64" or "amd64" or "x86_64" => "x64",
            "x86" or "i386" or "i686" => "x86",
            "arm64" or "aarch64" => "arm64",
            _ => processArch.Trim().ToLowerInvariant(),
        };

    private static string GetRuntimeRid(string osPlatform, string processArch, bool isMusl)
    {
        var architecture = NormalizeArchitecture(processArch);
        if (string.Equals(osPlatform, "Linux", StringComparison.OrdinalIgnoreCase) && isMusl)
        {
            return $"linux-musl-{architecture}";
        }

        var os = osPlatform.ToLowerInvariant() switch
        {
            "windows" => "win",
            "macos" => "osx",
            _ => osPlatform.ToLowerInvariant(),
        };
        return $"{os}-{architecture}";
    }

    private static string CreateUnsupportedArchitectureDiagnostic(string osPlatform, string processArch, bool isMusl) =>
        $"Datadog.Trace.BenchmarkDotNet {DatadogProfilerPackageVersion} does not ship a profiler asset for " +
        $"RID '{GetRuntimeRid(osPlatform, processArch, isMusl)}' (OS '{osPlatform}', architecture '{processArch}').";

    internal sealed record ProfilerAssetPaths(
        string MonitoringHome,
        string Rid,
        string SelectedTracerPath,
        string SelectedProfilerPath,
        string LoaderConfig,
        string? Profiler32Path,
        string? Profiler64Path);
}

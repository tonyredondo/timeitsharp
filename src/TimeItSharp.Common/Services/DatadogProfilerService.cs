using System.Runtime.InteropServices;
using System.Security.Cryptography;
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
    private const long MaximumLoaderFileBytes = 1024 * 1024;
    private const int MaximumLoaderRows = 4096;
    private const int MaximumLoaderRowLength = 16 * 1024;
    private const string TracerProfilerId = "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}";
    private const string NativeProfilerId = "{BD1A650D-AC5D-4896-B64F-D6FA25D6B26A}";

    // SHA-256 digests of the native assets published by Datadog.Trace.BenchmarkDotNet 2.61.0.
    // This is the provenance boundary for both packaged and inherited profiler homes: matching
    // names and a valid loader.conf are insufficient because a v3/native replacement can expose
    // the same layout and profiler CLSID.
    private static readonly IReadOnlyDictionary<string, string> TrustedProfilerAssetHashes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["linux-arm64/Datadog.Profiler.Native.so"] = "D7A83C592061620F0960161B5F8E6547F159A6747F14F840B1C5D264414080AE",
            ["linux-arm64/Datadog.Trace.ClrProfiler.Native.so"] = "E75176556CAFA7462FED44EE380466CE43E79A386693FB0D332F952B60B098DC",
            ["linux-arm64/loader.conf"] = "A7EDAF020F0F01431BDE4C8F429C88E7AEF3E23B3A710A74B66783AB9ACC13D7",
            ["linux-musl-x64/Datadog.Profiler.Native.so"] = "C490F81EADF532559F6F0990CF260A1A6CDA2EA0586FCCC2D54E61206062B653",
            ["linux-musl-x64/Datadog.Trace.ClrProfiler.Native.so"] = "6CF311DC8A30701A86A0A49B529B9766DBA4ABBECEE1A0AB0B4781BE58A13575",
            ["linux-musl-x64/loader.conf"] = "A7EDAF020F0F01431BDE4C8F429C88E7AEF3E23B3A710A74B66783AB9ACC13D7",
            ["linux-x64/Datadog.Profiler.Native.so"] = "3A9A8059BD81311C2DE38D4602AA94EA6175EB2901601CD5EADA529A0CC29EE8",
            ["linux-x64/Datadog.Trace.ClrProfiler.Native.so"] = "91F7ABA5E11750886195C84B0BFE9AB196FBF96C420ACF772BE306A9A90D1409",
            ["linux-x64/loader.conf"] = "A7EDAF020F0F01431BDE4C8F429C88E7AEF3E23B3A710A74B66783AB9ACC13D7",
            ["osx/loader.conf"] = "A7EDAF020F0F01431BDE4C8F429C88E7AEF3E23B3A710A74B66783AB9ACC13D7",
            ["win-x64/Datadog.Profiler.Native.dll"] = "BCC57BE6A8A35AB59D818F570F096AE0CEA1548204CFD088C6C7FA2470892E63",
            ["win-x64/Datadog.Trace.ClrProfiler.Native.dll"] = "058E5A0D80916DD014C6F15B3B372102C589CFD639D575F2ACD361C2406A5DB5",
            ["win-x64/loader.conf"] = "6F8F3C4A9A07790791F430BBEFDDD070B5D127FFC8DC8B5F56ACF67B0818ED37",
            ["win-x86/Datadog.Profiler.Native.dll"] = "07806EC88688852946D50218B05EBC1BF67A51EE7214019CB511C66D69A1EE95",
            ["win-x86/Datadog.Trace.ClrProfiler.Native.dll"] = "ACEBC5CB4D5FA179FA17F1C562EAA8B4D67C9AE814FFF52124806534FB8123C5",
            ["win-x86/loader.conf"] = "6F8F3C4A9A07790791F430BBEFDDD070B5D127FFC8DC8B5F56ACF67B0818ED37",
            ["net461/Datadog.Trace.dll"] = "F5DA8823D46D275AB06D4748A49521894ED2EF6F607BAA5BBAD225D2563A56AD",
            ["net6.0/Datadog.Trace.dll"] = "27DE362C525CDBC0356FFFD1E026E1C291F2B0A0DC5DC43BFBE30DCF30FBF419",
            ["netcoreapp3.1/Datadog.Trace.dll"] = "8B4498E0A0882818F28E1DD77ACE80AB95197889AC101A223954D2B96D23550F",
            ["netstandard2.0/Datadog.Trace.dll"] = "61E285BF54DD2AE2829EEB8ED08F3AD1BF6ABFA96D52A70CE6E56B5F7CCC5AD4",
        };

    private bool _environmentConfigured;
    private IReadOnlyDictionary<string, string?>? _profilerEnvironmentVariables;
    private IReadOnlyDictionary<string, string?> _hostEnvironment = new Dictionary<string, string?>();
    private DatadogProfilerConfiguration? _profilerConfiguration;
    private Config? _configuration;
    private IReadOnlyList<string> _knownSecretValues = Array.Empty<string>();
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
        var hostEnvironment = options.HostEnvironment ?? ScenarioProcessor.CaptureEnvironmentVariables();
        _hostEnvironment = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string?>(
            new Dictionary<string, string?>(hostEnvironment,
                OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal));
        _knownSecretValues = Utils.GetSensitiveEnvironmentValues(options.Configuration?.EnvironmentVariables)
            .Concat(Utils.GetTemplateSecretValues(options.TemplateVariables))
            .Concat(_hostEnvironment.Take(Utils.MaxTagCollectionItems)
                .Where(item => (Utils.IsSensitiveEnvironmentVariable(item.Key) ||
                                string.Equals(item.Key, "DD_DOTNET_TRACER_HOME", StringComparison.OrdinalIgnoreCase)) &&
                               !string.IsNullOrEmpty(item.Value) &&
                               item.Value.Length <= Utils.MaxExportLogCharacters)
                .Select(item => item.Value!))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
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
                AnsiConsole.WriteLine(Utils.SanitizeText(_profilerDiagnostic, _knownSecretValues));
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
            var envVar = MergeProfilerEnvironment(command.EnvironmentVariables, profilerEnvironmentVariables);
            DatadogMetadata.GetIds(scenario, out _, out var spanId);
            envVar["DD_INTERNAL_CIVISIBILITY_SPANID"] = spanId.ToString();
            command = command.WithEnvironmentVariables(envVar);
            _environmentConfigured = true;
            return;
        }

        // CliWrap replaces, rather than merges, the command environment. Keep the complete
        // benchmark environment (including credentials and other sensitive inputs) and add
        // explicit tombstones for every profiler selector so host values cannot leak into a
        // warmup, disabled scenario, or unsupported host.
        command = command.WithEnvironmentVariables(TombstoneProfilerEnvironment(command.EnvironmentVariables));
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

    internal static Dictionary<string, string?> MergeProfilerEnvironment(
        IReadOnlyDictionary<string, string?> commandEnvironment,
        IReadOnlyDictionary<string, string?> profilerEnvironment)
    {
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var environment = new Dictionary<string, string?>(commandEnvironment, comparer);

        // Remove all inherited selectors first. Mandatory values below select only the verified
        // v2.61 assets. Non-selector values supplied by the benchmark always win, including
        // DD_API_KEY/DD_CLIENT_TOKEN and custom variables classified as sensitive.
        foreach (var key in ProfilerEnvironmentVariableNames)
        {
            environment[key] = null;
        }

        foreach (var item in profilerEnvironment)
        {
            if (IsMandatoryProfilerVariable(item.Key) || !environment.ContainsKey(item.Key))
            {
                environment[item.Key] = item.Value;
            }
        }

        return environment;
    }

    internal static Dictionary<string, string?> TombstoneProfilerEnvironment(
        IReadOnlyDictionary<string, string?> commandEnvironment)
    {
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var environment = new Dictionary<string, string?>(commandEnvironment, comparer);
        foreach (var key in ProfilerEnvironmentVariableNames)
        {
            environment[key] = null;
        }

        return environment;
    }

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

    private Dictionary<string, string?>? GetProfilerEnvironmentVariables(out string? diagnostic)
    {
        diagnostic = null;
        try
        {
            var benchmarkDotNetVersion = typeof(Datadog.Trace.BenchmarkDotNet.DatadogDiagnoser)
                .Assembly.GetName().Version;
            if (benchmarkDotNetVersion != new Version(2, 61, 0, 0))
            {
                diagnostic = $"Datadog profiler integration requires BenchmarkDotNet package {DatadogProfilerPackageVersion}; " +
                             $"loaded version is '{benchmarkDotNetVersion?.ToString() ?? "unknown"}'.";
                return null;
            }

            var osPlatform = GetCurrentOsPlatform();
            var processArch = RuntimeInformation.ProcessArchitecture.ToString();
            var isMusl = IsMuslLinux();
            var diagnostics = new List<string>();
            var profilerHomes = GetProfilersHomeFolder()
                .Where(homePath => !string.IsNullOrWhiteSpace(homePath))
                .Take(4)
                .Select(homePath => homePath!)
                .ToArray();
            _knownSecretValues = _knownSecretValues
                .Concat(profilerHomes)
                .Where(value => value.Length <= Utils.MaxExportLogCharacters)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            foreach (var homePath in profilerHomes)
            {
                if (!Directory.Exists(homePath))
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
                        diagnostics.Add(Utils.SanitizeText(pathDiagnostic, _knownSecretValues));
                    }

                    continue;
                }

                return BuildProfilerEnvironment(profilerPaths!, _knownSecretValues);
            }

            diagnostic = diagnostics.Count == 0
                ? $"Datadog.Trace.BenchmarkDotNet {DatadogProfilerPackageVersion} profiler assets were not found for RID '{GetRuntimeRid(osPlatform, processArch, isMusl)}'."
                : string.Join(Environment.NewLine, diagnostics.Distinct(StringComparer.Ordinal).Take(3));
            return null;
        }
        catch (PlatformNotSupportedException exception)
        {
            diagnostic = Utils.SanitizeText(exception.Message, _knownSecretValues);
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            diagnostic = Utils.SanitizeText($"Datadog profiler asset discovery failed: {exception.Message}", _knownSecretValues);
            return null;
        }
    }

    private static Dictionary<string, string?> BuildProfilerEnvironment(
        ProfilerAssetPaths profilerPaths,
        IEnumerable<string>? knownSecretValues)
    {
        var tracer = Tracer.Instance;
        var environment = new Dictionary<string, string?>
        {
            [ConfigurationKeys.ServiceName] = Utils.SanitizeText(tracer.DefaultServiceName, knownSecretValues),
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
            environment[ConfigurationKeys.Environment] = Utils.SanitizeText(environmentInternal, knownSecretValues);
        }

        if (tracer.Settings.ServiceVersionInternal is { } serviceVersionInternal)
        {
            environment[ConfigurationKeys.ServiceVersion] = Utils.SanitizeText(serviceVersionInternal, knownSecretValues);
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
            // These values are copied into a child environment, not a report sink. Still redact
            // URI credentials and configured/template secrets before propagating CI metadata.
            environment["DD_GIT_REPOSITORY_URL"] = Utils.SanitizeText(ciEnv.Repository, knownSecretValues);
            environment["DD_GIT_COMMIT_SHA"] = Utils.SanitizeText(ciEnv.Commit, knownSecretValues);

            if (!string.IsNullOrEmpty(ciEnv.Branch))
            {
                tagsList.Add($"{CommonTags.GitBranch}:{Utils.SanitizeText(ciEnv.Branch, knownSecretValues)}");
            }

            if (!string.IsNullOrEmpty(ciEnv.Tag))
            {
                tagsList.Add($"{CommonTags.GitTag}:{Utils.SanitizeText(ciEnv.Tag, knownSecretValues)}");
            }
        }

        var newDdTags = Utils.SanitizeText(string.Join(", ", tagsList), knownSecretValues);
        if (newDdTags.Length > Utils.MaxTagCharacters)
        {
            newDdTags = Utils.RedactedValue;
        }

        environment["DD_TAGS"] = environment.TryGetValue("DD_TAGS", out var ddTags)
            ? Utils.SanitizeText(newDdTags + "," + ddTags, knownSecretValues)
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

    private IEnumerable<string?> GetProfilersHomeFolder()
    {
        // Use the run-entry snapshot. Reading EnvironmentHelpers here would allow another
        // extension or concurrent run to change profiler selection after validation.
        _hostEnvironment.TryGetValue("DD_DOTNET_TRACER_HOME", out var configuredHome);
        yield return configuredHome;

        // Then locate TimeItSharp's isolated, exact v2 home. Assembly.Location is empty in
        // single-file hosts, and a relative/CWD candidate would let an attacker win profiler
        // selection by planting a profiler directory in the working directory.
        var assemblyLocation = typeof(Datadog.Trace.BenchmarkDotNet.DatadogDiagnoser).Assembly.Location;
        if (!string.IsNullOrWhiteSpace(assemblyLocation) && Path.IsPathRooted(assemblyLocation))
        {
            yield return Path.Combine(Path.GetDirectoryName(assemblyLocation) ?? string.Empty, "datadog-v2");
        }

        if (Path.IsPathRooted(AppDomain.CurrentDomain.BaseDirectory))
        {
            yield return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "datadog-v2");
        }
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

        if (!TryValidateProfilerHome(monitoringHome, out diagnostic))
        {
            return false;
        }

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

            // loader.conf is RID-relative. Advertising both bitness paths with the selected
            // RID's single loader file can make a cross-bitness child resolve an incoherent row.
            // Support only the host/current-child contract and leave the opposite path tombstoned.
            if (normalizedArch == "x86")
            {
                profiler32Path = selectedTracerPath;
            }
            else
            {
                profiler64Path = selectedTracerPath;
            }
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

        if (!TryValidateProfilerHomeAssets(monitoringHome, out diagnostic))
        {
            return false;
        }

        if (!IsTrustedProfilerAsset(selectedTracerPath, rid, out diagnostic) ||
            !IsTrustedProfilerAsset(selectedProfilerPath, rid, out diagnostic) ||
            !IsTrustedProfilerAsset(loaderConfig, rid, out diagnostic))
        {
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
                return;
            }

            if (!IsRegularProfilerFile(path))
            {
                missingAssets.Add($"{description} '{path}' is not a regular trusted file");
            }

            var parent = Path.GetDirectoryName(path);
            if (parent is null || !IsRegularProfilerDirectory(parent))
            {
                missingAssets.Add($"{description} '{path}' is under an untrusted directory");
            }
        }
    }

    private static bool IsRegularProfilerFile(string path)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            return (attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) == 0 &&
                   new FileInfo(path).LinkTarget is null &&
                   HasTrustedDirectoryChain(Path.GetDirectoryName(Path.GetFullPath(path)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static bool IsRegularProfilerDirectory(string path)
    {
        try
        {
            return HasTrustedDirectoryChain(Path.GetFullPath(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static bool HasTrustedDirectoryChain(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        for (var directory = new DirectoryInfo(path); directory is not null; directory = directory.Parent)
        {
            if (!directory.Exists)
            {
                return false;
            }

            var attributes = directory.Attributes;
            if ((attributes & FileAttributes.Directory) == 0 ||
                (attributes & FileAttributes.ReparsePoint) != 0 ||
                directory.LinkTarget is not null)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryValidateProfilerHomeAssets(string monitoringHome, out string diagnostic)
    {
        diagnostic = string.Empty;
        try
        {
            var pathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            var discovered = new Dictionary<string, string>(pathComparer);
            var pending = new Stack<string>();
            pending.Push(monitoringHome);
            var entryCount = 0;
            while (pending.Count > 0)
            {
                var directory = pending.Pop();
                foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
                {
                    if (++entryCount > 4096)
                    {
                        diagnostic = $"Datadog profiler home '{monitoringHome}' exceeds the trusted content limit.";
                        return false;
                    }

                    var attributes = File.GetAttributes(entry);
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        diagnostic = $"Datadog profiler home '{monitoringHome}' contains a symlink or reparse point '{entry}'.";
                        return false;
                    }

                    if ((attributes & FileAttributes.Directory) != 0)
                    {
                        if (new DirectoryInfo(entry).LinkTarget is not null)
                        {
                            diagnostic = $"Datadog profiler home '{monitoringHome}' contains a linked directory '{entry}'.";
                            return false;
                        }

                        pending.Push(entry);
                        continue;
                    }

                    var fileName = Path.GetFileName(entry);
                    if (fileName.Contains("ApiWrapper", StringComparison.OrdinalIgnoreCase) ||
                        fileName.Contains("Datadog.Trace.Bundle", StringComparison.OrdinalIgnoreCase))
                    {
                        diagnostic = $"Datadog profiler home '{monitoringHome}' contains incompatible v3 asset '{entry}'.";
                        return false;
                    }

                    var relative = Path.GetRelativePath(monitoringHome, entry)
                        .Replace(Path.DirectorySeparatorChar, '/');
                    if (!discovered.TryAdd(relative, entry))
                    {
                        diagnostic = $"Datadog profiler home '{monitoringHome}' contains duplicate asset '{relative}'.";
                        return false;
                    }
                }
            }

            var expected = new HashSet<string>(TrustedProfilerAssetHashes.Keys, pathComparer);
            if (!expected.SetEquals(discovered.Keys))
            {
                var missing = expected.Except(discovered.Keys, pathComparer)
                    .OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToArray();
                var unexpected = discovered.Keys.Except(expected, pathComparer)
                    .OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToArray();
                diagnostic =
                    $"Datadog profiler home '{monitoringHome}' does not contain the exact {DatadogProfilerPackageVersion} asset set. " +
                    $"Missing: {string.Join(", ", missing)}; unexpected: {string.Join(", ", unexpected)}.";
                return false;
            }

            foreach (var asset in discovered)
            {
                if (!IsTrustedProfilerAssetByManifestPath(asset.Value, asset.Key, out diagnostic))
                {
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            diagnostic = $"Could not validate Datadog profiler assets under '{monitoringHome}': {ex.Message}";
            return false;
        }
    }

    private static bool IsTrustedProfilerAsset(string path, string rid, out string diagnostic) =>
        IsTrustedProfilerAssetByManifestPath(path, $"{rid}/{Path.GetFileName(path)}", out diagnostic);

    private static bool IsTrustedProfilerAssetByManifestPath(string path, string assetName, out string diagnostic)
    {
        diagnostic = string.Empty;
        if (!TrustedProfilerAssetHashes.TryGetValue(assetName, out var expectedHash))
        {
            diagnostic = $"Datadog profiler asset '{assetName}' is not in the trusted {DatadogProfilerPackageVersion} manifest.";
            return false;
        }

        try
        {
            if (!IsRegularProfilerFile(path))
            {
                diagnostic = $"Datadog profiler asset '{path}' is not a regular file under a trusted directory chain.";
                return false;
            }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var sha256 = SHA256.Create();
            var actualHash = Convert.ToHexString(sha256.ComputeHash(stream));
            if (!string.Equals(actualHash, expectedHash, StringComparison.Ordinal))
            {
                diagnostic =
                    $"Datadog profiler asset '{path}' does not match the trusted {DatadogProfilerPackageVersion} SHA-256 manifest.";
                return false;
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            diagnostic = $"Could not validate Datadog profiler asset '{path}': {ex.Message}";
            return false;
        }
    }

    private static bool TryValidateProfilerHome(string monitoringHome, out string diagnostic)
    {
        diagnostic = string.Empty;
        try
        {
            if (!Path.IsPathRooted(monitoringHome))
            {
                diagnostic = $"Datadog profiler home '{monitoringHome}' must be an absolute trusted path.";
                return false;
            }

            var fullHome = Path.GetFullPath(monitoringHome);
            if (!IsRegularProfilerDirectory(fullHome))
            {
                diagnostic =
                    $"Datadog profiler home '{monitoringHome}' is missing or has a symlink/reparse-point ancestor.";
                return false;
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            diagnostic = $"Could not validate Datadog profiler home '{monitoringHome}': {ex.Message}";
            return false;
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
            if (!IsRegularProfilerFile(loaderConfig) ||
                new FileInfo(loaderConfig).Length > MaximumLoaderFileBytes)
            {
                diagnostic = $"Datadog profiler loader '{loaderConfig}' is missing, too large, or is not a regular trusted file.";
                return false;
            }

            var loaderDirectory = Path.GetDirectoryName(loaderConfig) ?? string.Empty;
            // Datadog's v2 loader.conf is shared-format and lists rows for other
            // architectures too. Only the selected RID row must resolve inside this
            // RID directory; requiring every row would reject the package it ships.
            var expectedPath = Path.GetFullPath(expectedProfilerPath);
            var found = false;
            var rowCount = 0;
            foreach (var rawLine in File.ReadLines(loaderConfig))
            {
                if (++rowCount > MaximumLoaderRows || rawLine.Length > MaximumLoaderRowLength)
                {
                    diagnostic = $"Datadog profiler loader '{loaderConfig}' exceeds the supported size or row limits.";
                    return false;
                }

                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                {
                    continue;
                }

                var fields = line.Split(';');
                if (fields.Length < 4)
                {
                    diagnostic = $"Datadog profiler loader '{loaderConfig}' contains a malformed row.";
                    return false;
                }

                // BenchmarkDotNet 2.61.0 carries optional TRACER rows, but this service never
                // activates them (and the package intentionally does not ship Datadog.Tracer.Native
                // assets). Only PROFILER rows are part of this v2 profiler contract.
                if (string.Equals(fields[0], "TRACER", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.Equals(fields[0], "PROFILER", StringComparison.OrdinalIgnoreCase) ||
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
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
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

    internal static bool IsMuslLinux()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return false;
        }

        // Inspect libraries mapped into this process. Unlike probing well-known loader paths,
        // this reports the libc actually hosting the current runtime when both glibc and musl
        // happen to be installed in the same image.
        try
        {
            var mappedLibc = DetectMuslFromProcMaps(File.ReadLines("/proc/self/maps").Take(16 * 1024));
            if (mappedLibc is not null)
            {
                return mappedLibc.Value;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // Fall through to an in-process native symbol check.
        }

        IntPtr libc = IntPtr.Zero;
        try
        {
            if (NativeLibrary.TryLoad("libc.so.6", out libc))
            {
                return !NativeLibrary.TryGetExport(libc, "gnu_get_libc_version", out _);
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or BadImageFormatException or NotSupportedException)
        {
            // RuntimeIdentifier is the final fallback for restricted /proc containers.
        }
        finally
        {
            if (libc != IntPtr.Zero)
            {
                NativeLibrary.Free(libc);
            }
        }

        return RuntimeInformation.RuntimeIdentifier.Contains("musl", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool? DetectMuslFromProcMaps(IEnumerable<string> mappedLibraries)
    {
        var sawGlibc = false;
        foreach (var line in mappedLibraries)
        {
            var pathStart = line.IndexOf('/');
            if (pathStart < 0)
            {
                continue;
            }

            var mappedPath = line[pathStart..].Trim();
            const string deletedSuffix = " (deleted)";
            if (mappedPath.EndsWith(deletedSuffix, StringComparison.Ordinal))
            {
                mappedPath = mappedPath[..^deletedSuffix.Length];
            }

            var fileName = Path.GetFileName(mappedPath);
            if (IsSharedObjectName(fileName, "ld-musl-") ||
                IsSharedObjectName(fileName, "libc.musl-"))
            {
                return true;
            }

            if (fileName.Equals("libc.so.6", StringComparison.Ordinal) ||
                IsSharedObjectName(fileName, "ld-linux-"))
            {
                sawGlibc = true;
            }
        }

        return sawGlibc ? false : null;
    }

    private static bool IsSharedObjectName(string fileName, string prefix)
    {
        if (!fileName.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var soIndex = fileName.IndexOf(".so", prefix.Length, StringComparison.Ordinal);
        if (soIndex <= prefix.Length)
        {
            return false;
        }

        var suffix = fileName[(soIndex + 3)..];
        if (suffix.Length == 0)
        {
            return true;
        }

        return suffix[0] == '.' && suffix.Length > 1 &&
               suffix[1..].Split('.').All(segment =>
                   segment.Length > 0 && segment.All(char.IsDigit));
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

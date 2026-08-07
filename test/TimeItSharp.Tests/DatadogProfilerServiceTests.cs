using TimeItSharp.Common.Services;

namespace TimeItSharp.Tests;

public sealed class DatadogProfilerServiceTests
{
    [Fact]
    public void Linux_x64_uses_the_v2_linux_x64_asset_set_without_preload_wrapper()
    {
        var home = CreateProfilerHome("linux-x64", "linux-x64");
        try
        {
            var found = DatadogProfilerService.TryGetProfilerPaths(
                home,
                "Linux",
                "X64",
                isMusl: false,
                out var paths,
                out var diagnostic);

            Assert.True(found, diagnostic);
            Assert.NotNull(paths);
            Assert.EndsWith(Path.Combine("linux-x64", "Datadog.Trace.ClrProfiler.Native.so"), paths!.SelectedTracerPath);
            Assert.EndsWith(Path.Combine("linux-x64", "Datadog.Profiler.Native.so"), paths.SelectedProfilerPath);
            Assert.Null(paths.Profiler32Path);
            Assert.DoesNotContain("ApiWrapper", diagnostic, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    [Fact]
    public void Linux_musl_x64_selects_the_musl_rid_and_generic_loader_row()
    {
        var home = CreateProfilerHome("linux-musl-x64", "linux-x64");
        try
        {
            var found = DatadogProfilerService.TryGetProfilerPaths(
                home,
                "Linux",
                "x86_64",
                isMusl: true,
                out var paths,
                out var diagnostic);

            Assert.True(found, diagnostic);
            Assert.Equal("linux-musl-x64", paths!.Rid);
            Assert.Contains("linux-musl-x64", paths.SelectedTracerPath, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    [Fact]
    public void Linux_arm64_musl_reports_the_missing_v2_rid_instead_of_falling_back_to_x64()
    {
        var home = Path.Combine(AppContext.BaseDirectory, $"timeitsharp-profiler-{Guid.NewGuid():N}");
        Directory.CreateDirectory(home);
        try
        {
            var found = DatadogProfilerService.TryGetProfilerPaths(
                home,
                "Linux",
                "Arm64",
                isMusl: true,
                out var paths,
                out var diagnostic);

            Assert.False(found);
            Assert.Null(paths);
            Assert.Contains("linux-musl-arm64", diagnostic, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    [Fact]
    public void Missing_profiler_asset_does_not_report_a_valid_environment()
    {
        var home = CreateProfilerHome("linux-x64", "linux-x64");
        File.Delete(Path.Combine(home, "linux-x64", "Datadog.Profiler.Native.so"));
        try
        {
            var found = DatadogProfilerService.TryGetProfilerPaths(
                home,
                "Linux",
                "X64",
                isMusl: false,
                out var paths,
                out var diagnostic);

            Assert.False(found);
            Assert.Null(paths);
            Assert.Contains("continuous profiler", diagnostic, StringComparison.Ordinal);
            Assert.Contains("linux-x64", diagnostic, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    [Fact]
    public void Profiled_environment_preserves_credentials_and_sensitive_benchmark_inputs()
    {
        var commandEnvironment = new Dictionary<string, string?>
        {
            ["DD_API_KEY"] = "benchmark-api-key",
            ["MY_PASSWORD"] = "benchmark-password",
            ["DD_TAGS"] = "benchmark:tag",
            ["CORECLR_PROFILER_PATH"] = "/untrusted/v3.so",
        };
        var profilerEnvironment = new Dictionary<string, string?>
        {
            ["DD_API_KEY"] = "service-value-must-not-win",
            ["MY_PASSWORD"] = "service-value-must-not-win",
            ["DD_TAGS"] = "service:tag",
            ["CORECLR_PROFILER_PATH"] = "/trusted/v2.so",
            ["CORECLR_ENABLE_PROFILING"] = "1",
        };

        var merged = DatadogProfilerService.MergeProfilerEnvironment(commandEnvironment, profilerEnvironment);

        Assert.Equal("benchmark-api-key", merged["DD_API_KEY"]);
        Assert.Equal("benchmark-password", merged["MY_PASSWORD"]);
        Assert.Equal("benchmark:tag", merged["DD_TAGS"]);
        Assert.Equal("/trusted/v2.so", merged["CORECLR_PROFILER_PATH"]);
        Assert.Equal("1", merged["CORECLR_ENABLE_PROFILING"]);
    }

    [Fact]
    public void Non_profiled_environment_preserves_full_command_environment_and_tombstones_profiler_keys()
    {
        var commandEnvironment = new Dictionary<string, string?>
        {
            ["DD_API_KEY"] = "benchmark-api-key",
            ["MY_PASSWORD"] = "benchmark-password",
            ["CUSTOM"] = "value",
            ["EXPLICITLY_UNSET"] = null,
            ["CORECLR_ENABLE_PROFILING"] = "1",
        };

        var cleared = DatadogProfilerService.TombstoneProfilerEnvironment(commandEnvironment);

        Assert.Equal("benchmark-api-key", cleared["DD_API_KEY"]);
        Assert.Equal("benchmark-password", cleared["MY_PASSWORD"]);
        Assert.Equal("value", cleared["CUSTOM"]);
        Assert.Null(cleared["EXPLICITLY_UNSET"]);
        Assert.Null(cleared["CORECLR_ENABLE_PROFILING"]);
        Assert.Null(cleared["DD_DOTNET_TRACER_HOME"]);
        Assert.Null(cleared["DD_NATIVELOADER_CONFIGFILE"]);
    }

    [Fact]
    public void Modified_same_name_asset_is_rejected_by_the_v261_manifest()
    {
        var home = CreateProfilerHome("linux-x64", "linux-x64");
        try
        {
            File.AppendAllText(Path.Combine(home, "linux-x64", "Datadog.Trace.ClrProfiler.Native.so"), "v3");

            var found = DatadogProfilerService.TryGetProfilerPaths(
                home, "Linux", "X64", isMusl: false, out var paths, out var diagnostic);

            Assert.False(found);
            Assert.Null(paths);
            Assert.Contains("SHA-256", diagnostic, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    [Fact]
    public void Symlink_ancestor_is_rejected()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var realHome = CreateProfilerHome("linux-x64", "linux-x64");
        var realParent = Directory.GetParent(realHome)!.FullName;
        var linkRoot = Path.Combine(AppContext.BaseDirectory, $"timeitsharp-profiler-link-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateSymbolicLink(linkRoot, realParent);
            var found = DatadogProfilerService.TryGetProfilerPaths(
                Path.Combine(linkRoot, Path.GetFileName(realHome)),
                "Linux", "X64", isMusl: false, out var paths, out var diagnostic);

            Assert.False(found);
            Assert.Null(paths);
            Assert.Contains("symlink/reparse-point ancestor", diagnostic, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(linkRoot) || File.Exists(linkRoot))
            {
                Directory.Delete(linkRoot);
            }
            Directory.Delete(realHome, recursive: true);
        }
    }

    [Fact]
    public void Current_libc_detection_uses_mapped_libraries_not_installed_loader_files()
    {
        Assert.False(DatadogProfilerService.DetectMuslFromProcMaps(
            ["7f00-7fff /usr/lib/x86_64-linux-gnu/libc.so.6"]));
        Assert.True(DatadogProfilerService.DetectMuslFromProcMaps(
            ["7f00-7fff /lib/ld-musl-x86_64.so.1"]));
        Assert.Null(DatadogProfilerService.DetectMuslFromProcMaps(["[heap]"]));
    }

    private static string CreateProfilerHome(string rid, string loaderRid)
    {
        _ = loaderRid; // The canonical package loader contains the required RID row.
        var source = Path.Combine(AppContext.BaseDirectory, "datadog", rid);
        Assert.True(Directory.Exists(source), $"Missing test profiler assets: {source}");
        var home = Path.Combine(AppContext.BaseDirectory, $"timeitsharp-profiler-{Guid.NewGuid():N}");
        Directory.CreateDirectory(home);
        FileSystemHelpers.CopyDirectory(source, Path.Combine(home, rid));
        return home;
    }

    private static class FileSystemHelpers
    {
        public static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (var file in Directory.EnumerateFiles(source))
            {
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
            }
        }
    }
}

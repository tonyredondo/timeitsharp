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
        var home = Path.Combine(Path.GetTempPath(), $"timeitsharp-profiler-{Guid.NewGuid():N}");
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

    private static string CreateProfilerHome(string rid, string loaderRid)
    {
        var home = Path.Combine(Path.GetTempPath(), $"timeitsharp-profiler-{Guid.NewGuid():N}");
        var ridDirectory = Path.Combine(home, rid);
        Directory.CreateDirectory(ridDirectory);
        File.WriteAllBytes(Path.Combine(ridDirectory, "Datadog.Profiler.Native.so"), []);
        File.WriteAllBytes(Path.Combine(ridDirectory, "Datadog.Trace.ClrProfiler.Native.so"), []);
        File.WriteAllText(
            Path.Combine(ridDirectory, "loader.conf"),
            $"PROFILER;{{BD1A650D-AC5D-4896-B64F-D6FA25D6B26A}};{loaderRid};./Datadog.Profiler.Native.so{Environment.NewLine}");
        return home;
    }
}

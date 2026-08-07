#!/usr/bin/env bash
set -euo pipefail

# Verify the package as a clean consumer, rather than relying on project references.
# Usage: verify-common-package.sh [package-directory] [version]
package_dir=${1:-artifacts}
version=${2:-0.4.8}
package_dir=$(cd "$package_dir" && pwd)
runtime=${TIMEIT_CONSUMER_RUNTIME:-$(dotnet --info | awk '/^ RID:/{print $2; exit}')}
if [[ -z "$runtime" ]]; then
  echo "Could not determine the host runtime identifier; set TIMEIT_CONSUMER_RUNTIME." >&2
  exit 1
fi
work_dir=$(mktemp -d)
trap 'rm -rf "$work_dir"' EXIT
# Keep the consumer restore isolated from any package cache in the repository host.
export NUGET_PACKAGES="$work_dir/packages"

mkdir -p "$work_dir/src" "$work_dir/target"
cat > "$work_dir/target/Target.csproj" <<'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
EOF
cat > "$work_dir/target/Program.cs" <<'EOF'
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

var profilerHome = Environment.GetEnvironmentVariable("DD_DOTNET_TRACER_HOME");
var enabled = Environment.GetEnvironmentVariable("CORECLR_ENABLE_PROFILING") == "1";
var marker = Environment.GetEnvironmentVariable("TIMEIT_PROFILER_PROBE");
bool attached = false;
string detail = string.Empty;
if (OperatingSystem.IsLinux() && enabled && !string.IsNullOrWhiteSpace(profilerHome))
{
    try
    {
        // Trigger a commonly instrumented method and allow the managed loader to initialize.
        using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMilliseconds(250) };
        try { await client.GetAsync("http://127.0.0.1:1"); } catch { }

        var expectedManaged = Path.Combine(profilerHome, "net6.0", "Datadog.Trace.dll");
        using var expectedStream = File.OpenRead(expectedManaged);
        using var peReader = new PEReader(expectedStream);
        var metadata = peReader.GetMetadataReader();
        var expectedMvid = metadata.GetGuid(metadata.GetModuleDefinition().Mvid);
        System.Reflection.Assembly? managed = null;
        for (var attempt = 0; attempt < 150 && managed is null; attempt++)
        {
            managed = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(assembly => assembly.GetName().Name == "Datadog.Trace");
            if (managed is null) await Task.Delay(100);
        }

        var maps = await File.ReadAllTextAsync("/proc/self/maps");
        var expectedTracer = Path.GetFullPath(Environment.GetEnvironmentVariable("CORECLR_PROFILER_PATH")!);
        var expectedProfiler = Path.Combine(Path.GetDirectoryName(expectedTracer)!, "Datadog.Profiler.Native.so");
        var location = managed?.Location;
        var locationIsPrivate = string.IsNullOrEmpty(location) ||
            Path.GetFullPath(location).StartsWith(Path.GetFullPath(profilerHome) + Path.DirectorySeparatorChar, StringComparison.Ordinal);
        attached = managed?.ManifestModule.ModuleVersionId == expectedMvid && locationIsPrivate &&
                   expectedTracer.StartsWith(Path.GetFullPath(profilerHome) + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
                   maps.Contains(expectedTracer, StringComparison.Ordinal) &&
                   maps.Contains(expectedProfiler, StringComparison.Ordinal);
        detail = $"managed={managed?.ManifestModule.ModuleVersionId};expected={expectedMvid};location={location};tracer={expectedTracer};profiler={expectedProfiler}";
    }
    catch (Exception exception)
    {
        detail = exception.ToString();
    }
}
else
{
    attached = !OperatingSystem.IsLinux();
    detail = $"platform={Environment.OSVersion.Platform};enabled={enabled};home={profilerHome}";
}

if (!string.IsNullOrWhiteSpace(marker))
{
    await File.WriteAllTextAsync(marker, $"attached={attached}{Environment.NewLine}{detail}");
}
Console.WriteLine("package-consumer-target");
// The parent consumer owns the attach assertion so it can print the complete probe diagnostics
// without first converting the target process into an unrelated failed data point.
EOF
cat > "$work_dir/NuGet.config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$package_dir" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
EOF

cat > "$work_dir/src/Consumer.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <RuntimeIdentifier>$runtime</RuntimeIdentifier>
    <SelfContained>true</SelfContained>
    <PublishSingleFile>true</PublishSingleFile>
    <PublishTrimmed>true</PublishTrimmed>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <!-- Common invokes CliWrap from trim-sensitive code paths; root the package so the
         framework facade dependencies remain available in a trimmed consumer. -->
    <TrimmerRootAssembly Include="CliWrap" />
    <PackageReference Include="TimeItSharp.Common" Version="$version" />
  </ItemGroup>
</Project>
EOF

cat > "$work_dir/src/Program.cs" <<'EOF'
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using TimeItSharp.Common;
using TimeItSharp.Common.Configuration.Builder;
using TimeItSharp.Common.Services;

internal static class Program
{
    [RequiresUnreferencedCode("This consumer smoke test intentionally exercises reflection-based extension loading.")]
    private static async System.Threading.Tasks.Task<int> Main(string[] args)
    {
        var root = AppContext.BaseDirectory;
        var hook = Path.Combine(root, "TimeItSharp.StartupHook.dll");
        if (!File.Exists(hook))
        {
            Console.Error.WriteLine($"Missing startup hook: {hook}");
            return 1;
        }

        var expected = new[]
        {
            Path.Combine("datadog-v2", "linux-x64", "Datadog.Profiler.Native.so"),
            Path.Combine("datadog-v2", "linux-x64", "Datadog.Trace.ClrProfiler.Native.so"),
            Path.Combine("datadog-v2", "linux-x64", "loader.conf"),
            Path.Combine("datadog-v2", "linux-musl-x64", "Datadog.Profiler.Native.so"),
            Path.Combine("datadog-v2", "linux-musl-x64", "Datadog.Trace.ClrProfiler.Native.so"),
            Path.Combine("datadog-v2", "linux-musl-x64", "loader.conf"),
            Path.Combine("datadog-v2", "linux-arm64", "Datadog.Profiler.Native.so"),
            Path.Combine("datadog-v2", "linux-arm64", "Datadog.Trace.ClrProfiler.Native.so"),
            Path.Combine("datadog-v2", "linux-arm64", "loader.conf"),
            Path.Combine("datadog-v2", "net461", "Datadog.Trace.dll"),
            Path.Combine("datadog-v2", "net6.0", "Datadog.Trace.dll"),
            Path.Combine("datadog-v2", "netcoreapp3.1", "Datadog.Trace.dll"),
            Path.Combine("datadog-v2", "netstandard2.0", "Datadog.Trace.dll"),
        };

        foreach (var relativePath in expected)
        {
            var path = Path.Combine(root, relativePath);
            if (!File.Exists(path))
            {
                Console.Error.WriteLine($"Missing Datadog v2 asset: {path}");
                return 1;
            }
        }

        if (Directory.EnumerateFiles(Path.Combine(root, "datadog-v2"), "*ApiWrapper*", SearchOption.AllDirectories).Any())
        {
            Console.Error.WriteLine("Unexpected incompatible Datadog API wrapper asset.");
            return 1;
        }

        var target = Path.GetFullPath(Path.Combine(root, "..", "target-publish", "Target"));
        if (!File.Exists(target))
        {
            Console.Error.WriteLine($"Missing target executable: {target}");
            return 1;
        }

        var output = Path.Combine(root, "consumer-results.json");
        var profilerProbe = Path.Combine(root, "profiler-probe.txt");
        var builder = ConfigBuilder.Create()
            .WithName("package-consumer")
            .WithService<DatadogProfilerService>()
            .WithEnvironmentVariables(new Dictionary<string, string>
            {
                ["TIMEIT_PROFILER_PROBE"] = profilerProbe,
                ["DD_PROFILING_ENABLED"] = "1",
                ["DD_TRACE_ENABLED"] = "1",
            })
            .WithProcessName(target)
            .WithProcessArguments("--target")
            .WithWorkingDirectory(root)
            .WithMetrics(true)
            .WithMetricsFrequency(200)
            .WithWarmupCount(0)
            .WithCount(1)
            .WithScenario(scenario => scenario.WithName("startup-hook-target"));
        var configuration = builder.Build();
        configuration.JsonExporterFilePath = output;

        var exitCode = await TimeItEngine.RunAsync(configuration).ConfigureAwait(false);
        if (exitCode != 0 || !File.Exists(output))
        {
            Console.Error.WriteLine($"TimeItSharp consumer run failed (exit code {exitCode}).");
            return 1;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(output));
        var scenarios = document.RootElement;
        if (scenarios.ValueKind != JsonValueKind.Array || scenarios.GetArrayLength() == 0)
        {
            Console.Error.WriteLine("Consumer result did not contain a scenario.");
            return 1;
        }

        var metricsData = scenarios[0].GetProperty("metricsData");
        if (metricsData.ValueKind != JsonValueKind.Object || !metricsData.EnumerateObject().Any())
        {
            Console.Error.WriteLine("Startup hook metrics were not observed by the Common package consumer.");
            return 1;
        }

        if (OperatingSystem.IsLinux() &&
            (!File.Exists(profilerProbe) || !File.ReadAllText(profilerProbe).StartsWith("attached=True", StringComparison.OrdinalIgnoreCase)))
        {
            Console.Error.WriteLine($"Datadog v2 profiler did not attach: {(File.Exists(profilerProbe) ? File.ReadAllText(profilerProbe) : "missing probe")}");
            return 1;
        }

        Console.WriteLine("TimeItSharp.Common package assets and trimmed startup-hook execution verified.");
        return 0;
    }
}
EOF


# Publish a minimal self-contained managed target for the selected RID so the consumer
# verifies actual startup-hook loading without depending on the host's shared runtime.
target_publish="$work_dir/target-publish"
dotnet publish "$work_dir/target/Target.csproj" --configuration Release --runtime "$runtime" --self-contained true \
  -p:PublishSingleFile=false -p:PublishTrimmed=false -p:PublishDir="$target_publish/"
dotnet restore "$work_dir/src/Consumer.csproj" --configfile "$work_dir/NuGet.config"
dotnet build "$work_dir/src/Consumer.csproj" --configuration Release --no-restore
dotnet publish "$work_dir/src/Consumer.csproj" --configuration Release --runtime "$runtime" --self-contained true \
  -p:PublishSingleFile=true -p:PublishTrimmed=true -p:PublishDir="$work_dir/publish/" \
  --configfile "$work_dir/NuGet.config"
v2_source="$NUGET_PACKAGES/datadog.trace.benchmarkdotnet/2.61.0/contentFiles/any/any/datadog"
trace_source="$NUGET_PACKAGES/datadog.trace/2.61.0/lib"
common_payload="$NUGET_PACKAGES/timeitsharp.common/$version/timeitsharp-assets/datadog-v2"
if [[ ! -d "$v2_source" || ! -d "$trace_source" || ! -d "$common_payload" ]]; then
  echo "Missing restored v2 native, managed, or private Common payload." >&2
  exit 1
fi

compare_complete_v2_home() {
  local destination=$1
  while IFS= read -r -d '' source_file; do
    local relative=${source_file#"$v2_source"/}
    local destination_file="$destination/$relative"
    if [[ ! -f "$destination_file" ]] || ! cmp -s "$source_file" "$destination_file"; then
      echo "Datadog v2 native content mismatch: $destination_file" >&2
      exit 1
    fi
  done < <(find "$v2_source" -type f -print0)
  while IFS= read -r -d '' source_file; do
    local relative=${source_file#"$trace_source"/}
    local destination_file="$destination/$relative"
    if [[ ! -f "$destination_file" ]] || ! cmp -s "$source_file" "$destination_file"; then
      echo "Datadog v2 managed content mismatch: $destination_file" >&2
      exit 1
    fi
  done < <(find "$trace_source" -mindepth 2 -maxdepth 2 -name Datadog.Trace.dll -type f -print0)
  local file_count
  file_count=$(find "$destination" -type f | wc -l | tr -d ' ')
  if [[ "$file_count" != 20 ]]; then
    echo "Datadog v2 home is not the exact 20-file set: $destination ($file_count files)" >&2
    exit 1
  fi
}

compare_complete_v2_home "$common_payload"
compare_complete_v2_home "$work_dir/src/bin/Release/net8.0/$runtime/datadog-v2"
compare_complete_v2_home "$work_dir/publish/datadog-v2"
cmp "$NUGET_PACKAGES/timeitsharp.common/$version/timeitsharp-assets/TimeItSharp.StartupHook.dll"     "$work_dir/publish/TimeItSharp.StartupHook.dll"
"$work_dir/publish/Consumer"

# Package a wrapper whose consumer has no direct Common reference. This proves that the
# buildTransitive imports carry the private startup hook and v2 payload across a package edge.
mkdir -p "$work_dir/wrapper" "$work_dir/wrapper-feed" "$work_dir/collision"
cat > "$work_dir/wrapper/TimeItSharp.Common.TestWrapper.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <PackageId>TimeItSharp.Common.TestWrapper</PackageId>
    <Version>1.0.0</Version>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="TimeItSharp.Common" Version="[$version]" />
  </ItemGroup>
</Project>
EOF
dotnet pack "$work_dir/wrapper/TimeItSharp.Common.TestWrapper.csproj" --configuration Release   --output "$work_dir/wrapper-feed" --configfile "$work_dir/NuGet.config"

cat > "$work_dir/collision/Collision.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <RuntimeIdentifier>$runtime</RuntimeIdentifier>
    <SelfContained>true</SelfContained>
    <PublishSingleFile>true</PublishSingleFile>
    <PublishTrimmed>false</PublishTrimmed>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="TimeItSharp.Common.TestWrapper" Version="[1.0.0]" />
    <PackageReference Include="Datadog.Trace.Bundle" Version="[3.50.0]" />
  </ItemGroup>
</Project>
EOF
cp "$work_dir/src/Program.cs" "$work_dir/collision/Program.cs"
cat > "$work_dir/Collision.NuGet.config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="wrapper" value="$work_dir/wrapper-feed" />
    <add key="local" value="$package_dir" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
EOF

dotnet restore "$work_dir/collision/Collision.csproj" --configfile "$work_dir/Collision.NuGet.config"
dotnet build "$work_dir/collision/Collision.csproj" --configuration Release --no-restore   --output "$work_dir/collision-bin"
dotnet publish "$work_dir/collision/Collision.csproj" --configuration Release --no-restore   --output "$work_dir/collision-publish"

v3_source="$NUGET_PACKAGES/datadog.trace.bundle/3.50.0/contentFiles/any/any/datadog"
if [[ ! -d "$v3_source" ]] || ! find "$v3_source" -iname '*ApiWrapper*' -print -quit | grep -q .; then
  echo "The v3 collision fixture did not contribute API-wrapper content." >&2
  exit 1
fi
if cmp -s "$trace_source/net6.0/Datadog.Trace.dll" "$v3_source/net6.0/Datadog.Trace.dll"; then
  echo "The v2/v3 managed collision fixture is not meaningful: bytes are equal." >&2
  exit 1
fi
compare_complete_v2_home "$work_dir/collision-bin/datadog-v2"
compare_complete_v2_home "$work_dir/collision-publish/datadog-v2"
if find "$work_dir/collision-publish/datadog-v2" -iname '*ApiWrapper*' -print -quit | grep -q .; then
  echo "The isolated v2 profiler home contains an API wrapper." >&2
  exit 1
fi
# The direct v3 bundle must remain intact in its own public home while TimeIt selects datadog-v2.
for relative in   net6.0/Datadog.Trace.dll   linux-x64/Datadog.Trace.ClrProfiler.Native.so   linux-x64/Datadog.Profiler.Native.so; do
  cmp "$v3_source/$relative" "$work_dir/collision-bin/datadog/$relative"
done
# Managed v3 content may be bundled by single-file publish, but native v3 content remains external.
for relative in   linux-x64/Datadog.Trace.ClrProfiler.Native.so   linux-x64/Datadog.Profiler.Native.so; do
  cmp "$v3_source/$relative" "$work_dir/collision-publish/datadog/$relative"
done
if ! find "$work_dir/collision-publish/datadog" -iname '*ApiWrapper*' -print -quit | grep -q .; then
  echo "Expected isolated v3 API-wrapper content was not published." >&2
  exit 1
fi
cmp "$NUGET_PACKAGES/timeitsharp.common/$version/timeitsharp-assets/TimeItSharp.StartupHook.dll"     "$work_dir/collision-publish/TimeItSharp.StartupHook.dll"
"$work_dir/collision-publish/Collision"
echo "Wrapper buildTransitive and isolated v2/v3 profiler homes verified."

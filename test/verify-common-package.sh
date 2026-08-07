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
Console.WriteLine("package-consumer-target");
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
using System.Text.Json;
using TimeItSharp.Common;
using TimeItSharp.Common.Configuration.Builder;

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
            Path.Combine("datadog", "linux-x64", "Datadog.Profiler.Native.so"),
            Path.Combine("datadog", "linux-x64", "Datadog.Trace.ClrProfiler.Native.so"),
            Path.Combine("datadog", "linux-x64", "loader.conf"),
            Path.Combine("datadog", "linux-musl-x64", "Datadog.Profiler.Native.so"),
            Path.Combine("datadog", "linux-musl-x64", "Datadog.Trace.ClrProfiler.Native.so"),
            Path.Combine("datadog", "linux-musl-x64", "loader.conf"),
            Path.Combine("datadog", "linux-arm64", "Datadog.Profiler.Native.so"),
            Path.Combine("datadog", "linux-arm64", "Datadog.Trace.ClrProfiler.Native.so"),
            Path.Combine("datadog", "linux-arm64", "loader.conf"),
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

        if (Directory.EnumerateFiles(root, "*ApiWrapper*", SearchOption.AllDirectories).Any())
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
        var builder = ConfigBuilder.Create()
            .WithName("package-consumer")
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
"$work_dir/publish/Consumer"

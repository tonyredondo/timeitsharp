# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What Is This

TimeItSharp is a .NET CLI benchmarking tool (`dotnet timeit`) that executes CLI applications multiple times to measure and compare performance metrics. It supports multiple scenarios, statistical analysis, and integrates with Datadog CI Test Visibility.

## Build & Development Commands

```bash
dotnet restore                                                          # Restore dependencies
dotnet build TimeItSharp.sln -c Release                                 # Build all projects
dotnet run --project test/TimeItSharp.FluentConfiguration.Sample -c Release  # Run the sample (used as CI test)
dotnet pack TimeItSharp.sln -c Release -o artifacts                     # Create NuGet packages
```

Smoke-test the tool: `./src/TimeItSharp/bin/Release/<tfm>/TimeItSharp config-example.json`

There are no unit tests yet. CI runs the fluent configuration sample across net6.0–net10.0 as the primary validation. New test coverage should use xUnit in `test/`.

## Architecture

**Plugin system** — Three extension interfaces in `TimeItSharp.Common`:
- `IExporter` — output results (ConsoleExporter, JsonExporter, DatadogExporter)
- `IAssertor` — validate results (DefaultAssertor)
- `IService` — lifecycle hooks (ExecuteService, DatadogProfilerService)

Plugins are loaded via assembly-qualified names from JSON config and initialized with `InitOptions`.

**Execution flow**: `Program.cs` (CLI via System.CommandLine) → `TimeItEngine.RunAsync()` (orchestrator) → `ScenarioProcessor` (per-scenario execution via CliWrap) → assertors validate → exporters output.

**Configuration**: JSON-based (`Config.cs`) with template variable interpolation (`TemplateVariables`), or fluent builder API (`ConfigBuilder`/`ScenarioBuilder`/`TimeoutBuilder`).

**StartupHook** (`src/TimeItSharp.StartupHook`): Targets `netcoreapp3.1` specifically for legacy .NET app compatibility. Collects runtime metrics via EventListener and binary file storage. Do not upgrade its target framework without confirming downstream host compatibility.

## Key Dependencies

- **CliWrap** — process execution
- **MathNet.Numerics** — statistical calculations (confidence intervals, outlier detection)
- **Spectre.Console** — formatted console output
- **System.CommandLine** (beta) — CLI argument parsing
- **Datadog.Trace.BenchmarkDotNet** — Datadog CI integration

## Coding Conventions

- 4-space indentation, PascalCase for public symbols, camelCase for locals/parameters
- Use `var` when the inferred type is obvious; honor nullable annotations
- Keep APIs trimming/AOT friendly
- Version and shared properties live in `src/Directory.Build.props`
- Language version: C# 13, target frameworks: net6.0 through net10.0

## Commits & PRs

- Short sentence-style commit subjects with PR references: `Fix unhandled exception (#71)`
- Squash feature branches to keep `main` linear
- Flag Datadog-impacting changes for dashboard verification

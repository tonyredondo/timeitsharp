# Repository Guidelines
## Project Structure & Module Organization
CLI tool lives in `src/TimeItSharp` (ships `dotnet timeit` and `config-example.json`). Shared engine components sit in `src/TimeItSharp.Common`; extend assertors, exporters, services there. Startup instrumentation sits in `src/TimeItSharp.StartupHook`; `test/TimeItSharp.FluentConfiguration.Sample` covers net6.0–net10.0. Build outputs and `.nupkg` artifacts collect in `artifacts/`.

## Configuration & Extensibility Tips
JSON configs expand tokens through `TemplateVariables` defined in `TimeItSharp.Common/TemplateVariables.cs`. Favor the fluent builder for repetitive setups and keep custom configs close to the CLI project or reference explicitly. Custom exporters, assertors, or services implement the matching interface in `TimeItSharp.Common`, register via assembly-qualified names, and surface options through `InitOptions`.

## Build, Test & Development Commands
- `dotnet restore` – restore solution dependencies.
- `dotnet build TimeItSharp.sln -c Release` – compile tool, common library, startup hook, and sample.
- `dotnet run --project test/TimeItSharp.FluentConfiguration.Sample -c Release` – execute the engine sample as CI does.
- `./TimeItSharp config-example.json` (from `src/TimeItSharp/bin/Release/<tfm>`) – smoke-test the packaged tool.
- `dotnet pack TimeItSharp.sln -c Release -o artifacts` – produce publishable `.nupkg` bundles.

## Release & Packaging Flow
Version and shared properties live in `src/Directory.Build.props`; bump it before release tagging. CI performs multi-target builds, runs the sample, and uploads artifacts—mirror that locally and publish only the `.nupkg` files in `artifacts/` for the CLI, common library, and startup hook.

## Coding Style & Naming Conventions
Adopt four-space indentation, PascalCase for public symbols, camelCase for locals and parameters. Favor `var` when the inferred type is obvious and honor nullable annotations. Keep APIs trimming/AOT friendly and update `Directory.Build.props` when newer language features are required.

## Startup Hook Caveats
`TimeItSharp.StartupHook` targets `netcoreapp3.1` to reach legacy profiled apps—upgrade only after confirming downstream hosts. Packaging relies on `<None Pack="true">` entries in the CLI and common projects, so include new resources there and retest standalone hook loading plus tool-triggered runs to catch trimming or single-file issues.

## Testing & Benchmarking Guidelines
Testing currently relies on executable samples. New coverage should use xUnit projects in `test/` named `<Component>.Tests`. Until then, keep `dotnet build` and the fluent configuration sample green on every TFM, archive JSON outputs from `src/TimeItSharp/bin/Release/<tfm>`, and tune warmups/counts to satisfy `DefaultAssertor` instead of disabling checks.

## Commit & Pull Request Expectations
Follow existing history: short, sentence-style commit subjects with optional PR references (e.g., `Fix unhandled exception (#71)`). Squash feature branches to keep `main` linear. PRs should motivate the change, summarize configuration updates, attach release-sample snippets, link issues, and flag Datadog-impacting changes for dashboard verification.

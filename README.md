# TimeItSharp
[![NuGet](https://img.shields.io/nuget/v/TimeItSharp.svg)](https://www.nuget.org/packages/TimeItSharp)
[![.NET](https://github.com/tonyredondo/timeitsharp/actions/workflows/ci.yml/badge.svg)](https://github.com/tonyredondo/timeitsharp/actions/workflows/ci.yml)

Command execution time meter allows to configure multiple scenarios to run benchmarks over CLI apps, output are available in markdown and json.

### Install
```bash
dotnet tool install --global TimeItSharp
```

### Usage
```bash
dotnet timeit [configuration file.json]
```

or

```bash
dotnet timeit -- "[command]"
```

```bash
❯ dotnet timeit --help
TimeItSharp v0.1.20
Description:

Usage:
  TimeItSharp <configuration file or process name> [options]

Arguments:
  <configuration file or process name>  The JSON configuration file or process name

Options:
  --variable <variable>        Variables used to instantiate the configuration file [default: TimeItSharp.Common.TemplateVariables]
  --count <count>              Number of iterations to run
  --warmup <warmup>            Number of iterations to warm up
  --metrics                    Enable Metrics from startup hook [default: True]
  --json-exporter              Enable JSON exporter [default: False]
  --datadog-exporter           Enable Datadog exporter [default: False]
  --datadog-profiler           Enable Datadog profiler [default: False]
  --first-run-stdout           Show the StdOut and StdErr for the first run [default: False]
  --process-failed-executions  Include failed executions in the final results [default: False]
  --debug                      Run timeit in debug mode [default: False]
  --version                    Show version information
  -?, -h, --help               Show help and usage information
```


#### Default Configuration when running a command
```
Warmup count = 1
Count = 10
Exporters = ConsoleExporter
Assertors = DefaultAssertor
```

## Sample Configuration

```json
{
  "enableDatadog": true,
  "enableMetrics": true,
  "warmUpCount": 10,
  "count": 100,
  "assertors": [
    {
      "name": "DefaultAssertor"
    }
  ],
  "services": [
    {
      "name": "NoopService"
    },
    {
      "name": "DatadogProfiler",
      "options": {
        "useExtraRun": true,
        "extraRunCount": 5,
        "scenarios" : [ "CallTarget" ]
      }
    },
    {
      "name": "Execute",
      "options": {
        "onScenarioStart": {
          "processName": "echo",
          "processArguments": "*** onScenarioStart ***",
          "workingDirectory": "$(CWD)/",
          "redirectStandardOutput": true
        },
        "onScenarioFinish": {
          "processName": "echo",
          "processArguments": "*** onScenarioFinish ***",
          "workingDirectory": "$(CWD)/",
          "redirectStandardOutput": true
        },
        "onExecutionStart": {
          "processName": "echo",
          "processArguments": "*** onExecutionStart ***",
          "workingDirectory": "$(CWD)/"
        },
        "onExecutionEnd": {
          "processName": "echo",
          "processArguments": "*** onExecutionEnd ***",
          "workingDirectory": "$(CWD)/"
        },
        "afterAllScenariosFinishes": {
          "processName": "echo",
          "processArguments": "*** afterAllScenariosFinishes ***",
          "workingDirectory": "$(CWD)/",
          "redirectStandardOutput": true
        },
        "onFinish": {
          "processName": "echo",
          "processArguments": "*** onFinish ***",
          "workingDirectory": "$(CWD)/",
          "redirectStandardOutput": true
        }
      }
    }
  ],
  "scenarios": [
    {
      "name": "Callsite",
      "isBaseline": true,
      "environmentVariables": {
        "DD_TRACE_CALLTARGET_ENABLED": "false",
        "DD_CLR_ENABLE_INLINING": "false"
      }
    },
    {
      "name": "CallTarget",
      "environmentVariables": {
        "DD_TRACE_CALLTARGET_ENABLED": "true",
        "DD_CLR_ENABLE_INLINING": "false"
      }
    },
    {
      "name": "CallTarget\u002BInlining",
      "environmentVariables": {
        "DD_TRACE_CALLTARGET_ENABLED": "true",
        "DD_CLR_ENABLE_INLINING": "true"
      }
    }
  ],
  "processName": "dotnet",
  "processArguments": "--version",
  "workingDirectory": "$(CWD)/",
  "environmentVariables": {
    "CORECLR_ENABLE_PROFILING": "1",
    "CORECLR_PROFILER": "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}",
    "CORECLR_PROFILER_PATH": "/Datadog.Trace.ClrProfiler.Native.dylib",
    "DD_DOTNET_TRACER_HOME": "/",
    "DD_INTEGRATIONS": "/integrations.json"
  },
  "tags": {
    "runtime.architecture" : "x86",
    "runtime.name" : ".NET Framework",
    "runtime.version" : "4.6.1",
    "benchmark.job.runtime.name" : ".NET Framework 4.6.1",
    "benchmark.job.runtime.moniker" : "net461"
  },
  "timeout" : {
    "maxDuration": 15,
    "processName": "dotnet-dump",
    "processArguments": "collect --process-id %pid%"
  },
  "overheadThreshold": 0.1
}
```

## Sample output

```bash
dotnet timeit config-example.json

TimeItSharp v0.4.0
Warmup count: 10
Max count: 100
Acceptable relative width: 0,7%
Confidence level: 95%
Minimum error reduction: 0,1%
Maximum duration: 45min
Overhead threshold: 10%
Number of Scenarios: 3
Exporters: ConsoleExporter, JsonExporter, Datadog
Assertors: DefaultAssertor
Services: NoopService, DatadogProfiler, Execute

*** onScenarioStart ***
ExecuteService.OnScenarioStart: ProcessId: 57272, ProcessName: echo, Duration: 00:00:00.0114420, ExitCode: 0
Scenario: Callsite
  Cmd: dotnet --version
  Warming up ..........
    Duration: 1 sec 894 ms
  Run ....................................................................................................
    Duration: 17 sec 610 ms

*** onScenarioFinish ***
ExecuteService.OnScenarioFinish: ProcessId: 57762, ProcessName: echo, Duration: 00:00:00.0029140, ExitCode: 0
*** onScenarioStart ***
ExecuteService.OnScenarioStart: ProcessId: 57763, ProcessName: echo, Duration: 00:00:00.0021860, ExitCode: 0
Scenario: CallTarget
  Cmd: dotnet --version
  Warming up ..........
    Duration: 1 sec 783 ms
  Run ....................................................................................................
    Duration: 17 sec 575 ms

*** onScenarioFinish ***
ExecuteService.OnScenarioFinish: ProcessId: 58246, ProcessName: echo, Duration: 00:00:00.0023170, ExitCode: 0
*** onScenarioStart ***
ExecuteService.OnScenarioStart: ProcessId: 58247, ProcessName: echo, Duration: 00:00:00.0061310, ExitCode: 0
Scenario: CallTarget+Inlining
  Cmd: dotnet --version
  Warming up ..........
    Duration: 1 sec 777 ms
  Run ......................................................................................
    Acceptable relative width criteria met. Stopping iterations for this scenario.
    N: 82
    Mean: 82,05ms
    Confidence Interval at 95: [81,764ms, 82,336ms]. Relative width: 0,6971%
    Duration: 15 sec 141 ms

*** onScenarioFinish ***
ExecuteService.OnScenarioFinish: ProcessId: 58713, ProcessName: echo, Duration: 00:00:00.0023110, ExitCode: 0
*** afterAllScenariosFinishes ***
ExecuteService.AfterAllScenariosFinishes: ProcessId: 58714, ProcessName: echo, Duration: 00:00:00.0022520, ExitCode: 0
### Results (last 10):
                                               
| Callsite | CallTarget | CallTarget+Inlining |
| :------: | :--------: | :-----------------: |
| 80,743ms |  83,044ms  |      80,732ms       |
| 81,672ms |  81,522ms  |      82,712ms       |
| 82,224ms |  80,891ms  |      81,288ms       |
| 82,71ms  |  80,833ms  |      81,287ms       |
| 82,256ms |  80,549ms  |       81,53ms       |
| 83,502ms |  80,168ms  |       80,64ms       |
| 82,368ms |  80,494ms  |      81,656ms       |
| 82,076ms |  82,498ms  |      84,044ms       |
| 81,51ms  |  83,453ms  |      81,702ms       |
| 85,344ms |  81,94ms   |      81,283ms       |
                                               
### Outliers (last 5):
                                               
| Callsite | CallTarget | CallTarget+Inlining |
| :------: | :--------: | :-----------------: |
| 87,907ms |  87,308ms  |      87,211ms       |
| 87,434ms |  90,669ms  |      84,566ms       |
| 90,536ms |  89,403ms  |       88,14ms       |
| 88,04ms  |  88,565ms  |      84,275ms       |
| 90,688ms |  88,601ms  |       80,44ms       |
                                               
### Distribution:

                            ┌ ██████████                               (5)
       79,95ms - 80,53ms    ┤ ▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒                     (10)
                            └                                          (0)
                            ┌ ██████████████                           (7)
       80,53ms - 81,10ms    ┤ ▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒           (15)
                            └ ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░ (20)
                            ┌ ████████████████████████████████████     (18)
       81,10ms - 81,68ms    ┤ ▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒         (16)
                            └ ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░   (19)
                            ┌ ██████████████████████████████████████   (19)
       81,68ms - 82,25ms    ┤ ▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒       (17)
                            └ ░░░░░░░░░░░░░░                           (7)
                            ┌ ██████████████████████                   (11)
       82,25ms - 82,83ms    ┤ ▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒                         (8)
                            └ ░░░░░░░░░░░░░░░░░░░░░░                   (11)
                            ┌ ████████████                             (6)
       82,83ms - 83,40ms    ┤ ▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒                         (8)
                            └ ░░░░░░░░                                 (4)
                            ┌ ████████████                             (6)
       83,40ms - 83,98ms    ┤ ▒▒▒▒▒▒                                   (3)
                            └ ░░░░░░░░░░░░                             (6)
                            ┌ ████████                                 (4)
       83,98ms - 84,56ms    ┤ ▒▒▒▒▒▒▒▒                                 (4)
                            └ ░░░░░░░░                                 (4)
                            ┌ ████████                                 (4)
       84,56ms - 85,13ms    ┤ ▒▒▒▒                                     (2)
                            └                                          (0)
                            ┌ ██████                                   (3)
       85,13ms - 85,71ms    ┤ ▒▒▒▒                                     (2)
                            └                                          (0)
                            ┌ ██████                                   (3)
       85,71ms - 86,28ms    ┤ ▒▒                                       (1)
                            └                                          (0)
  Legend:
    █ : Callsite  Width: 6,14ms
    ▒ : CallTarget  Width: 5,99ms
    ░ : CallTarget+Inlining  Width: 3,53ms

### Summary:
                                                                                                                                                                       
| Name                                        | Status | Mean     | StdDev  | StdErr  | Median   | C. Interval 100%     | C. Interval 95%      | Outliers | Overhead% |
| ------------------------------------------- | ------ | -------- | ------- | ------- | -------- | -------------------- | -------------------- | -------- | --------- |
| Callsite [N=100]                            | Passed | 82,47ms  | 1,468ms | 0,158ms | 82,132ms | [80,146 - 86,284] ms | [82,156 - 82,785] ms | 14 {1,2} | 0         |
|   ├>process.corrected_duration_ms           |        | 62.548   | 0.668   | 0.08    | 62.488   | [61.389 - 64.027]    | [62.387 - 62.708]    | 17 {1,2} |           |
|   ├>process.internal_duration_ms            |        | 31.943   | 0.356   | 0.042   | 31.846   | [31.437 - 32.768]    | [31.859 - 32.027]    | 11 {1,3} |           |
|   ├>process.startuphook_overhead_ms         |        | 19.63    | 0.228   | 0.027   | 19.558   | [19.251 - 20.173]    | [19.575 - 19.684]    | 13 {1,2} |           |
|   ├>process.time_to_end_main_ms             |        | 1.672    | 0.097   | 0.012   | 1.646    | [1.552 - 1.888]      | [1.649 - 1.695]      | 13 {1,2} |           |
|   ├>process.time_to_end_ms                  |        | 1.625    | 0.086   | 0.01    | 1.597    | [1.51 - 1.812]       | [1.604 - 1.646]      | 16 {1,2} |           |
|   ├>process.time_to_main_ms                 |        | 48.572   | 0.588   | 0.07    | 48.523   | [47.605 - 49.766]    | [48.432 - 48.712]    | 14 {1,3} |           |
|   ├>process.time_to_start_ms                |        | 28.86    | 0.346   | 0.042   | 28.847   | [28.241 - 29.591]    | [28.777 - 28.943]    | 14 {1,4} |           |
|   ├>runtime.dotnet.cpu.percent              |        | 1.438    | 0.022   | 0.002   | 1.45     | [1.4 - 1.45]         | [1.433 - 1.443]      | 13 {1,6} |           |
|   ├>runtime.dotnet.cpu.system               |        | 214.504  | 2.241   | 0.27    | 214.61   | [210.67 - 220.345]   | [213.966 - 215.043]  | 15 {1,3} |           |
|   ├>runtime.dotnet.cpu.user                 |        | 43.752   | 1.186   | 0.143   | 43.685   | [41.745 - 46.065]    | [43.467 - 44.037]    | 13 {1,4} |           |
|   ├>runtime.dotnet.mem.committed            |        | 0        | 0       | 0       | 0        | 0                    | 0                    | 0        |           |
|   ├>runtime.dotnet.threads.contention_count |        | 0        | 0       | 0       | 0        | 0                    | 0                    | 0        |           |
|   ├>runtime.dotnet.threads.contention_time  |        | 0        | 0       | 0       | 0        | 0                    | 0                    | 0        |           |
|   ├>runtime.dotnet.threads.count            |        | 13.5     | 0       | 0       | 13.5     | 13.5                 | 13.5                 | 0        |           |
|   ├>runtime.dotnet.threads.workers_count    |        | 0        | 0       | 0       | 0        | 0                    | 0                    | 0        |           |
|   ├>runtime.process.private_bytes           |        | 0        | 0       | 0       | 0        | 0                    | 0                    | 0        |           |
|   └>runtime.process.processor_time          |        | 170.794  | 1.839   | 0.223   | 170.202  | [168.41 - 175.15]    | [170.349 - 171.24]   | 15 {1,1} |           |
|                                             |        |          |         |         |          |                      |                      |          |           |
| CallTarget [N=100]                          | Passed | 81,959ms | 1,328ms | 0,143ms | 81,799ms | [79,95 - 85,938] ms  | [81,674 - 82,244] ms | 14 {0,8} | -0,62     |
|   ├>process.corrected_duration_ms           |        | 62.074   | 0.669   | 0.079   | 62.006   | [61.038 - 63.369]    | [61.916 - 62.232]    | 13 {1,3} |           |
|   ├>process.internal_duration_ms            |        | 31.74    | 0.286   | 0.034   | 31.744   | [31.334 - 32.358]    | [31.671 - 31.808]    | 14 {1,1} |           |
|   ├>process.startuphook_overhead_ms         |        | 19.476   | 0.202   | 0.023   | 19.456   | [19.149 - 19.968]    | [19.43 - 19.523]     | 9 {1,3}  |           |
|   ├>process.time_to_end_main_ms             |        | 1.606    | 0.077   | 0.009   | 1.595    | [1.5 - 1.798]        | [1.589 - 1.624]      | 14 {1,2} |           |
|   ├>process.time_to_end_ms                  |        | 1.556    | 0.064   | 0.007   | 1.551    | [1.456 - 1.726]      | [1.541 - 1.571]      | 16 {1,3} |           |
|   ├>process.time_to_main_ms                 |        | 48.287   | 0.492   | 0.06    | 48.202   | [47.569 - 49.378]    | [48.168 - 48.407]    | 15 {1,2} |           |
|   ├>process.time_to_start_ms                |        | 28.736   | 0.353   | 0.043   | 28.629   | [28.216 - 29.533]    | [28.651 - 28.821]    | 15 {1,2} |           |
|   ├>runtime.dotnet.cpu.percent              |        | 1.432    | 0.024   | 0.003   | 1.45     | [1.4 - 1.45]         | [1.426 - 1.437]      | 0        |           |
|   ├>runtime.dotnet.cpu.system               |        | 213.236  | 1.684   | 0.204   | 213.312  | [210.22 - 216.59]    | [212.828 - 213.643]  | 14 {1,2} |           |
|   ├>runtime.dotnet.cpu.user                 |        | 43.828   | 0.963   | 0.117   | 43.85    | [42.2 - 45.595]      | [43.595 - 44.061]    | 14 {1,3} |           |
|   ├>runtime.dotnet.mem.committed            |        | 0        | 0       | 0       | 0        | 0                    | 0                    | 0        |           |
|   ├>runtime.dotnet.threads.contention_count |        | 0        | 0       | 0       | 0        | 0                    | 0                    | 0        |           |
|   ├>runtime.dotnet.threads.contention_time  |        | 0        | 0       | 0       | 0        | 0                    | 0                    | 0        |           |
|   ├>runtime.dotnet.threads.count            |        | 13.5     | 0       | 0       | 13.5     | 13.5                 | 13.5                 | 0        |           |
|   ├>runtime.dotnet.threads.workers_count    |        | 0        | 0       | 0       | 0        | 0                    | 0                    | 0        |           |
|   ├>runtime.process.private_bytes           |        | 0        | 0       | 0       | 0        | 0                    | 0                    | 0        |           |
|   └>runtime.process.processor_time          |        | 169.708  | 0.652   | 0.081   | 169.645  | [168.515 - 171.07]   | [169.546 - 169.869]  | 16 {1,1} |           |
|                                             |        |          |         |         |          |                      |                      |          |           |
| CallTarget+Inlining [N=86]                  | Passed | 81,885ms | 1,036ms | 0,123ms | 81,469ms | [80,58 - 84,112] ms  | [81,64 - 82,131] ms  | 15 {1}   | -0,71     |
|   ├>process.corrected_duration_ms           |        | 62.113   | 0.636   | 0.083   | 61.988   | [61.226 - 63.45]     | [61.945 - 62.28]     | 13 {1,3} |           |
|   ├>process.internal_duration_ms            |        | 31.702   | 0.272   | 0.035   | 31.642   | [31.334 - 32.256]    | [31.632 - 31.773]    | 10 {1,2} |           |
|   ├>process.startuphook_overhead_ms         |        | 19.602   | 0.223   | 0.029   | 19.661   | [19.251 - 19.968]    | [19.545 - 19.659]    | 10 {1,4} |           |
|   ├>process.time_to_end_main_ms             |        | 1.649    | 0.088   | 0.011   | 1.646    | [1.519 - 1.816]      | [1.628 - 1.671]      | 14 {1,3} |           |
|   ├>process.time_to_end_ms                  |        | 1.603    | 0.073   | 0.009   | 1.577    | [1.503 - 1.736]      | [1.584 - 1.622]      | 12 {1,3} |           |
|   ├>process.time_to_main_ms                 |        | 48.141   | 0.403   | 0.052   | 48.082   | [47.563 - 48.937]    | [48.036 - 48.246]    | 13 {1,3} |           |
|   ├>process.time_to_start_ms                |        | 28.714   | 0.286   | 0.036   | 28.682   | [28.278 - 29.299]    | [28.641 - 28.787]    | 12 {1,4} |           |
|   ├>runtime.dotnet.cpu.percent              |        | 1.435    | 0.023   | 0.003   | 1.45     | [1.4 - 1.45]         | [1.43 - 1.44]        | 0        |           |
|   ├>runtime.dotnet.cpu.system               |        | 213.373  | 1.659   | 0.222   | 212.89   | [211.095 - 217.195]  | [212.929 - 213.818]  | 13 {1,2} |           |
|   ├>runtime.dotnet.cpu.user                 |        | 43.294   | 0.753   | 0.098   | 43.215   | [41.92 - 44.865]     | [43.098 - 43.491]    | 12 {1,5} |           |
|   ├>runtime.dotnet.mem.committed            |        | 0        | 0       | 0       | 0        | 0                    | 0                    | 0        |           |
|   ├>runtime.dotnet.threads.contention_count |        | 0        | 0       | 0       | 0        | 0                    | 0                    | 0        |           |
|   ├>runtime.dotnet.threads.contention_time  |        | 0        | 0       | 0       | 0        | 0                    | 0                    | 0        |           |
|   ├>runtime.dotnet.threads.count            |        | 13.5     | 0       | 0       | 13.5     | 13.5                 | 13.5                 | 0        |           |
|   ├>runtime.dotnet.threads.workers_count    |        | 0        | 0       | 0       | 0        | 0                    | 0                    | 0        |           |
|   ├>runtime.process.private_bytes           |        | 0        | 0       | 0       | 0        | 0                    | 0                    | 0        |           |
|   └>runtime.process.processor_time          |        | 169.958  | 1.423   | 0.189   | 169.49   | [168.215 - 173.645]  | [169.581 - 170.336]  | 13 {1,2} |           |
                                                                                                                                                                       

### Overheads:
                                                                                 
|                     | Callsite       | CallTarget       | CallTarget+Inlining |
| ------------------- | -------------- | ---------------- | ------------------- |
| Callsite            | --             | -0.6% (-0,511ms) | -0.7% (-0,585ms)    |
| CallTarget          | 0.6% (0,511ms) | --               | -0.1% (-0,074ms)    |
| CallTarget+Inlining | 0.7% (0,585ms) | 0.1% (0,074ms)   | --                  |
                                                                                 

The json file '/Users/tony.redondo/repos/github/tonyredondo/timeitsharp/src/TimeItSharp/bin/Release/net9.0/jsonexporter_991684479.json' was exported.
The Datadog exported ran successfully.
The Datadog profiler could not be attached to the .NET processes.
*** onFinish ***
ExecuteService.OnFinish: ProcessId: 58728, ProcessName: echo, Duration: 00:00:00.0022280, ExitCode: 0
```

## Output is markdown compatible

Example:

### Results:

|  Callsite  | CallTarget | CallTarget+Inlining |
| :--------: | :--------: | :-----------------: |
| 118,073ms  | 118,3071ms |     118,1759ms      |
| 119,1395ms | 120,4868ms |     119,1315ms      |
| 118,8491ms | 118,7805ms |     117,4429ms      |
| 119,4459ms | 118,166ms  |     116,6845ms      |
| 117,9159ms | 117,501ms  |     118,9928ms      |
| 118,4678ms | 119,4761ms |     120,3463ms      |
| 118,9661ms | 117,7897ms |     117,4329ms      |
| 119,121ms  | 118,5463ms |     120,0743ms      |
...

### Outliers:

|  Callsite  | CallTarget | CallTarget+Inlining |
| :--------: | :--------: | :-----------------: |
| 117,3012ms | 127,412ms  |     123,7949ms      |
| 117,4366ms | 129,8414ms |     129,5164ms      |
| 125,8246ms | 127,7837ms |     123,7299ms      |
| 120,6458ms | 127,9238ms |      128,343ms      |
| 126,3406ms | 128,2422ms |     128,1666ms      |
| 116,7321ms | 127,0036ms |     123,8186ms      |
| 123,4536ms | 127,7425ms |     124,5007ms      |
| 116,7023ms | 128,9796ms |      123,748ms      |
| 117,0362ms | 128,5864ms |     116,0693ms      |
| 121,2303ms | 131,3119ms |          -          |
| 122,5626ms | 139,5261ms |          -          |
| 117,1463ms | 132,8303ms |          -          |

### Summary:

| Name                                        | Status | Mean           | StdDev       | StdErr      | Min        | Median     | Max        | P95        | P90            | Outliers |
| ------------------------------------------- | ------ | -------------- | ------------ | ----------- | ---------- | ---------- | ---------- | ---------- | -------------- | -------- |
| Callsite                                    | Passed | 118,8135ms     | 0,7095ms     | 0,0778ms    | 117,5719ms | 118,7429ms | 120,4548ms | 120,0555ms | 119,8079ms     | 17 {1}   |
|   ├>process.corrected_duration_ms           |        | 86,140113      | 0,430977     | 0,052652    | 85,3643    | 86,1444    | 86,9631    | 86,87197   | 86,763167      | 16 {1,4} |
|   ├>process.internal_duration_ms            |        | 40,153966      | 0,256768     | 0,03069     | 39,7312    | 40,1408    | 40,5504    | 40,5504    | 40,512853      | 12 {1,3} |
|   ├>process.startuphook_overhead_ms         |        | 32,634311      | 0,294505     | 0,034708    | 32,1536    | 32,6656    | 33,0752    | 33,0752    | 33,0752        | 9 {1,4}  |
|   ├>process.time_to_end_main_ms             |        | 5,358497       | 0,078895     | 0,009498    | 5,2202     | 5,3753     | 5,5074     | 5,47168    | 5,454627       | 15 {1,4} |
|   ├>process.time_to_end_ms                  |        | 5,267668       | 0,073456     | 0,008843    | 5,1305     | 5,2769     | 5,405      | 5,38568    | 5,360673       | 15 {1,4} |
|   ├>process.time_to_main_ms                 |        | 73,25671       | 0,433434     | 0,051805    | 72,5972    | 73,203     | 74,0836    | 74,04103   | 73,92514       | 12 {1,4} |
|   ├>process.time_to_start_ms                |        | 40,630537      | 0,321633     | 0,038442    | 40,134     | 40,61995   | 41,2627    | 41,20579   | 41,082703      | 15 {1,4} |
|   ├>runtime.dotnet.cpu.percent              |        | 0,508929       | 0,179719     | 0,019609    | 0,25       | 0,5        | 0,75       | 0,75       | 0,75           | 0        |
|   ├>runtime.dotnet.cpu.system               |        | 312,5          | 0            | 0           | 312,5      | 312,5      | 312,5      | 312,5      | 312,5          | 18 {0,5} |
|   ├>runtime.dotnet.cpu.user                 |        | 150,173611     | 61,11054     | 6,441617    | 78,125     | 156,25     | 234,375    | 234,375    | 234,375        | 0        |
|   ├>runtime.dotnet.exceptions.count         |        | 1              | 0            | 0           | 1          | 1          | 1          | 1          | 1              | 0        |
|   ├>runtime.dotnet.mem.committed            |        | 6936606,567164 | 31066,602999 | 3795,389626 | 6893568    | 6930432    | 7012352    | 6998425,6  | 6987502,933333 | 14 {1,3} |
|   ├>runtime.dotnet.threads.contention_count |        | 0              | 0            | 0           | 0          | 0          | 0          | 0          | 0              | 0        |
|   ├>runtime.dotnet.threads.contention_time  |        | 0              | 0            | 0           | 0          | 0          | 0          | 0          | 0              | 0        |
|   ├>runtime.dotnet.threads.count            |        | 10             | 0            | 0           | 10         | 10         | 10         | 10         | 10             | 0        |
|   ├>runtime.dotnet.threads.workers_count    |        | 0              | 0            | 0           | 0          | 0          | 0          | 0          | 0              | 0        |
|   ├>runtime.process.private_bytes           |        | 6936606,567164 | 31066,602999 | 3795,389626 | 6893568    | 6930432    | 7012352    | 6998425,6  | 6987502,933333 | 14 {1,3} |
|   └>runtime.process.processor_time          |        | 159,040179     | 56,162117    | 6,127789    | 78,125     | 156,25     | 234,375    | 234,375    | 234,375        | 0        |
| CallTarget                                  | Passed | 120,0507ms     | 2,3859ms     | 0,2634ms    | 117,2969ms | 119,2402ms | 126,859ms  | 125,6954ms | 124,1969ms     | 18 {0,8} |
|   ├>process.corrected_duration_ms           |        | 86,764442      | 1,093408     | 0,128859    | 85,1422    | 86,5402    | 89,7589    | 88,721445  | 88,428037      | 13 {1,2} |
|   ├>process.internal_duration_ms            |        | 40,45907       | 0,647982     | 0,075326    | 39,5264    | 40,2432    | 42,0864    | 41,97888   | 41,495893      | 18 {1,2} |
|   ├>process.startuphook_overhead_ms         |        | 32,677472      | 0,498738     | 0,060041    | 31,9488    | 32,5632    | 33,792     | 33,5872    | 33,3824        | 16 {1,2} |
|   ├>process.time_to_end_main_ms             |        | 5,499259       | 0,136163     | 0,016512    | 5,283      | 5,47615    | 5,7631     | 5,741525   | 5,697067       | 16 {1,4} |
|   ├>process.time_to_end_ms                  |        | 5,449672       | 0,139421     | 0,016907    | 5,2332     | 5,4293     | 5,6888     | 5,677275   | 5,6505         | 16 {1,3} |
|   ├>process.time_to_main_ms                 |        | 73,445909      | 0,771463     | 0,094961    | 72,1542    | 73,2993    | 75,4407    | 75,0731    | 74,613523      | 15 {1,2} |
|   ├>process.time_to_start_ms                |        | 40,996214      | 0,606089     | 0,07193     | 40,0737    | 40,9266    | 42,4239    | 42,32766   | 41,984373      | 11 {1,3} |
|   ├>runtime.dotnet.cpu.percent              |        | 0,567269       | 0,177712     | 0,019506    | 0,25       | 0,5        | 0,75       | 0,75       | 0,75           | 11 {1,7} |
|   ├>runtime.dotnet.cpu.system               |        | 331,965488     | 33,758662    | 3,392873    | 312,5      | 312,5      | 390,625    | 390,625    | 390,625        | 0        |
|   ├>runtime.dotnet.cpu.user                 |        | 142,405063     | 60,978441    | 6,860611    | 78,125     | 156,25     | 234,375    | 234,375    | 234,375        | 15 {1,5} |
|   ├>runtime.dotnet.exceptions.count         |        | 1              | 0            | 0           | 1          | 1          | 1          | 1          | 1              | 0        |
|   ├>runtime.dotnet.mem.committed            |        | 6997926,956522 | 26336,175466 | 3170,502951 | 6955008    | 6995968    | 7077888    | 7059865,6  | 7024640        | 16 {1,3} |
|   ├>runtime.dotnet.threads.contention_count |        | 0              | 0            | 0           | 0          | 0          | 0          | 0          | 0              | 0        |
|   ├>runtime.dotnet.threads.contention_time  |        | 0              | 0            | 0           | 0          | 0          | 0          | 0          | 0              | 0        |
|   ├>runtime.dotnet.threads.count            |        | 10             | 0            | 0           | 10         | 10         | 10         | 10         | 10             | 0        |
|   ├>runtime.dotnet.threads.workers_count    |        | 0              | 0            | 0           | 0          | 0          | 0          | 0          | 0              | 0        |
|   ├>runtime.process.private_bytes           |        | 6997926,956522 | 26336,175466 | 3170,502951 | 6955008    | 6995968    | 7077888    | 7059865,6  | 7024640        | 16 {1,3} |
|   └>runtime.process.processor_time          |        | 177,271586     | 55,535049    | 6,095764    | 78,125     | 156,25     | 234,375    | 234,375    | 234,375        | 11 {1,7} |
| CallTarget+Inlining                         | Passed | 118,8732ms     | 1,1761ms     | 0,1232ms    | 116,6634ms | 118,8689ms | 122,2661ms | 120,8293ms | 120,3355ms     | 9 {1,3}  |
|   ├>process.corrected_duration_ms           |        | 86,347194      | 0,467871     | 0,05716     | 85,524     | 86,2929    | 87,3735    | 87,11181   | 86,977693      | 15 {1,4} |
|   ├>process.internal_duration_ms            |        | 40,042789      | 0,293506     | 0,035081    | 39,5264    | 40,0896    | 40,5504    | 40,448     | 40,448         | 17 {1,3} |
|   ├>process.startuphook_overhead_ms         |        | 32,494424      | 0,341204     | 0,041685    | 31,9488    | 32,4608    | 33,0752    | 33,0752    | 32,9728        | 16 {1,3} |
|   ├>process.time_to_end_main_ms             |        | 5,389477       | 0,094331     | 0,011195    | 5,2274     | 5,368      | 5,5813     | 5,55762    | 5,534767       | 13 {1,4} |
|   ├>process.time_to_end_ms                  |        | 5,323987       | 0,09187      | 0,010903    | 5,1824     | 5,3195     | 5,5054     | 5,47832    | 5,457473       | 13 {1,3} |
|   ├>process.time_to_main_ms                 |        | 73,50064       | 0,483363     | 0,059052    | 72,6678    | 73,4763    | 74,4952    | 74,30069   | 74,158827      | 16 {1,3} |
|   ├>process.time_to_start_ms                |        | 40,921329      | 0,375977     | 0,045262    | 40,3084    | 40,9231    | 41,6448    | 41,54568   | 41,449407      | 13 {1,4} |
|   ├>runtime.dotnet.cpu.percent              |        | 0,62234        | 0,225238     | 0,023232    | 0,25       | 0,5        | 1          | 1          | 1              | 0        |
|   ├>runtime.dotnet.cpu.system               |        | 312,5          | 0            | 0           | 312,5      | 312,5      | 312,5      | 312,5      | 312,5          | 18 {0,5} |
|   ├>runtime.dotnet.cpu.user                 |        | 119,298986     | 39,271643    | 4,565236    | 78,125     | 156,25     | 156,25     | 156,25     | 156,25         | 13 {1,1} |
|   ├>runtime.dotnet.exceptions.count         |        | 1              | 0            | 0           | 1          | 1          | 1          | 1          | 1              | 0        |
|   ├>runtime.dotnet.mem.committed            |        | 7087392,820513 | 47001,516683 | 5321,873791 | 7020544    | 7077888    | 7180288    | 7176192    | 7171413,333333 | 12 {1,4} |
|   ├>runtime.dotnet.threads.contention_count |        | 0              | 0            | 0           | 0          | 0          | 0          | 0          | 0              | 0        |
|   ├>runtime.dotnet.threads.contention_time  |        | 0              | 0            | 0           | 0          | 0          | 0          | 0          | 0              | 0        |
|   ├>runtime.dotnet.threads.count            |        | 10             | 0            | 0           | 10         | 10         | 10         | 10         | 10             | 0        |
|   ├>runtime.dotnet.threads.workers_count    |        | 0              | 0            | 0           | 0          | 0          | 0          | 0          | 0              | 0        |
|   ├>runtime.process.private_bytes           |        | 7087392,820513 | 47001,516683 | 5321,873791 | 7020544    | 7077888    | 7180288    | 7176192    | 7171413,333333 | 12 {1,4} |
|   └>runtime.process.processor_time          |        | 194,481383     | 70,386834    | 7,259848    | 78,125     | 156,25     | 312,5      | 312,5      | 312,5          | 0        |


### Overheads:

|                     | Callsite | CallTarget | CallTarget+Inlining |
| ------------------- | -------- | ---------- | ------------------- |
| Callsite            | --       | 1%         | 0.1%                |
| CallTarget          | -1%      | --         | -1%                 |
| CallTarget+Inlining | -0.1%    | 1%         | --                  |

## Datadog Exporter

The datadog exporter send all the data using the CI Test Visibility public api:

### Benchmark data
<img width="1519" alt="image" src="https://user-images.githubusercontent.com/69803/223069595-c6531c45-2085-4fbc-8d4f-79854c0ca58d.png">

### Metrics from the startup hook
<img width="818" alt="image" src="https://user-images.githubusercontent.com/69803/223069816-c3caf562-1cd2-46d3-8803-f42c6679647e.png">



## Datadog Profiler Service

The datadog profiler service injects the datadog profiler to the target .NET process

![image](https://github.com/tonyredondo/timeitsharp/assets/69803/757e9dad-9418-40f5-9fc6-d0e0881c08f6)

![image](https://github.com/tonyredondo/timeitsharp/assets/69803/b4b48267-c7f0-4e63-a4dd-a1a17f5c0f60)

![image](https://github.com/tonyredondo/timeitsharp/assets/69803/d54dbe17-bc89-4fd4-b985-79449ae62f29)


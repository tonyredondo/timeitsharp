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
  }
}
```

## Sample output

```bash
dotnet timeit config-example.json

TimeItSharp v0.1.20
Warmup count: 10
Count: 100
Number of Scenarios: 3
Exporters: ConsoleExporter, JsonExporter, Datadog
Assertors: DefaultAssertor
Services: NoopService, DatadogProfiler, Execute

*** onScenarioStart ***
ExecuteService.OnScenarioStart: ProcessId: 2251, ProcessName: echo, Duration: 00:00:00.0005176, ExitCode: 0
Scenario: Callsite
  Warming up ..........
    Duration: 1.168s
  Run ....................................................................................................
    Duration: 11.304s

*** onScenarioFinish ***
ExecuteService.OnScenarioFinish: ProcessId: 3688, ProcessName: echo, Duration: 00:00:00.0008148, ExitCode: 0
*** onScenarioStart ***
ExecuteService.OnScenarioStart: ProcessId: 3689, ProcessName: echo, Duration: 00:00:00.0006283, ExitCode: 0
Scenario: CallTarget
  Warming up ..........
    Duration: 1.125s
  Run ....................................................................................................
    Duration: 11.301s
  Run for 'DatadogProfiler' .....
    Duration: 0.571s

*** onScenarioFinish ***
ExecuteService.OnScenarioFinish: ProcessId: 5188, ProcessName: echo, Duration: 00:00:00.0007385, ExitCode: 0
*** onScenarioStart ***
ExecuteService.OnScenarioStart: ProcessId: 5189, ProcessName: echo, Duration: 00:00:00.0006489, ExitCode: 0
Scenario: CallTarget+Inlining
  Warming up ..........
    Duration: 1.119s
  Run ....................................................................................................
    Duration: 11.395s

*** onScenarioFinish ***
ExecuteService.OnScenarioFinish: ProcessId: 6624, ProcessName: echo, Duration: 00:00:00.0008311, ExitCode: 0
*** afterAllScenariosFinishes ***
ExecuteService.AfterAllScenariosFinishes: ProcessId: 6625, ProcessName: echo, Duration: 00:00:00.0006619, ExitCode: 0
### Results:
                                                 
|  Callsite  | CallTarget | CallTarget+Inlining |
| :--------: | :--------: | :-----------------: |
| 109.2346ms | 111.758ms  |     108.3788ms      |
| 109.457ms  | 107.6328ms |     112.7663ms      |
| 108.8787ms | 105.9236ms |      111.38ms       |
|  106.91ms  | 106.0436ms |     106.9646ms      |
| 111.9347ms | 105.9738ms |     107.9317ms      |
| 105.5839ms | 106.4764ms |      108.894ms      |
| 112.3722ms | 109.7447ms |     107.7772ms      |
| 112.7142ms | 105.9935ms |     108.8228ms      |
| 107.5852ms | 110.5389ms |     107.8372ms      |
| 109.3131ms | 109.6994ms |     108.5756ms      |
| 109.5149ms |  108.77ms  |     106.6446ms      |
| 107.2906ms | 111.1041ms |     108.6976ms      |
| 110.8005ms | 112.6504ms |      109.502ms      |
| 105.3984ms | 107.4712ms |     110.9537ms      |
| 107.7733ms | 106.5414ms |     107.9076ms      |
| 107.3503ms | 107.8755ms |     107.9666ms      |
| 108.1036ms | 107.3688ms |     108.8376ms      |
| 106.3998ms | 107.5925ms |     106.2671ms      |
| 108.841ms  | 110.6364ms |     112.7802ms      |
| 110.7411ms | 106.8128ms |     106.6458ms      |
| 107.1777ms | 109.6314ms |      106.918ms      |
| 107.7304ms | 108.6489ms |      109.133ms      |
| 107.0896ms | 107.9967ms |     105.7922ms      |
| 111.9551ms | 107.7906ms |     112.2041ms      |
| 110.7133ms | 107.545ms  |     109.9699ms      |
| 110.1093ms | 104.9762ms |     108.1229ms      |
| 107.0203ms | 106.6629ms |     106.8509ms      |
| 109.6226ms | 106.0384ms |     110.2012ms      |
| 109.8574ms | 105.7464ms |     108.0679ms      |
| 112.3784ms | 107.1098ms |     109.7055ms      |
| 106.1678ms | 110.2196ms |      108.384ms      |
| 108.4149ms | 107.3171ms |     110.6492ms      |
| 106.0006ms | 112.157ms  |     106.6742ms      |
| 109.8474ms | 109.6759ms |     108.8397ms      |
| 111.2227ms | 111.0033ms |     107.1974ms      |
| 109.7039ms | 107.2979ms |     111.9186ms      |
| 108.7286ms | 108.7697ms |      110.902ms      |
| 107.0425ms | 106.7501ms |     108.1721ms      |
| 108.6615ms | 106.7527ms |     107.6549ms      |
| 107.5691ms | 106.7101ms |      108.66ms       |
| 106.4025ms | 110.6951ms |     109.6942ms      |
| 106.7171ms | 111.1953ms |     105.9181ms      |
| 106.848ms  | 108.5743ms |     109.0042ms      |
| 105.6857ms | 107.706ms  |      111.806ms      |
| 106.1583ms | 106.4534ms |     106.2859ms      |
| 109.5718ms | 112.6558ms |     109.2141ms      |
| 110.9806ms | 106.3897ms |     107.9621ms      |
| 106.8286ms | 108.9212ms |      112.042ms      |
| 111.3773ms | 111.8226ms |     110.0811ms      |
| 109.7497ms | 109.111ms  |     105.8674ms      |
| 108.6592ms | 107.8792ms |      108.56ms       |
| 106.9794ms | 108.4423ms |     105.9217ms      |
| 110.5499ms | 109.8919ms |     107.6504ms      |
| 106.1873ms | 108.8743ms |     106.0812ms      |
| 109.1318ms | 111.5101ms |     106.4947ms      |
| 112.2361ms | 108.2432ms |     109.4954ms      |
| 106.2841ms | 109.4719ms |     107.7369ms      |
| 112.9023ms | 110.6768ms |     108.4328ms      |
| 110.9372ms | 110.0687ms |     108.1484ms      |
| 109.878ms  | 108.7001ms |     108.7459ms      |
| 110.3126ms | 107.1713ms |     108.3352ms      |
| 112.141ms  | 108.1855ms |     109.5345ms      |
| 109.498ms  | 108.4606ms |     106.9613ms      |
| 109.4807ms | 108.5445ms |     112.2276ms      |
| 107.5639ms | 110.0628ms |     106.6939ms      |
| 109.4453ms | 106.9689ms |     106.5953ms      |
| 111.7053ms | 106.8701ms |     111.9629ms      |
| 107.527ms  | 106.2626ms |     112.6658ms      |
| 108.9292ms | 107.364ms  |     110.1322ms      |
| 107.7035ms | 107.8079ms |     110.3307ms      |
| 107.8485ms | 105.9026ms |     108.0104ms      |
| 112.5987ms | 107.3507ms |     111.2351ms      |
| 108.1454ms | 106.9578ms |      109.105ms      |
| 110.6599ms | 105.6008ms |     110.9302ms      |
| 108.618ms  | 109.6726ms |     108.3211ms      |
| 112.9511ms | 107.2508ms |     106.2207ms      |
| 106.3289ms | 110.8919ms |     111.1861ms      |
| 109.5615ms | 109.9789ms |     109.1289ms      |
| 108.9674ms | 108.8175ms |     111.7899ms      |
| 112.2539ms | 108.5782ms |     110.8389ms      |
| 107.6025ms | 109.138ms  |     109.1483ms      |
| 106.8067ms | 111.1468ms |     108.5029ms      |
| 109.701ms  | 110.5281ms |     109.5014ms      |
| 107.8686ms | 107.0584ms |     111.9322ms      |
| 108.6323ms | 106.2539ms |     107.7478ms      |
|     -      | 109.3969ms |     111.1095ms      |
|     -      | 110.5424ms |     109.3917ms      |
|     -      | 108.0559ms |          -          |
|     -      |     -      |          -          |
|     -      |     -      |          -          |
|     -      |     -      |          -          |
|     -      |     -      |          -          |
|     -      |     -      |          -          |
|     -      |     -      |          -          |
|     -      |     -      |          -          |
|     -      |     -      |          -          |
|     -      |     -      |          -          |
|     -      |     -      |          -          |
|     -      |     -      |          -          |
|     -      |     -      |          -          |
                                                 
### Outliers:
                                                 
|  Callsite  | CallTarget | CallTarget+Inlining |
| :--------: | :--------: | :-----------------: |
| 113.2313ms | 113.8429ms |     126.9193ms      |
| 114.9186ms | 114.3078ms |     104.7638ms      |
| 103.9234ms | 104.6651ms |     113.1931ms      |
| 117.7886ms | 112.9772ms |      113.561ms      |
| 105.164ms  | 104.6341ms |     105.5102ms      |
| 113.9996ms | 104.8206ms |     123.9977ms      |
| 113.3056ms | 116.4127ms |     114.5014ms      |
| 104.5954ms | 114.234ms  |     105.1209ms      |
| 114.0536ms | 135.7914ms |     105.2464ms      |
| 104.7394ms | 103.5618ms |     104.9456ms      |
| 114.6633ms | 114.8296ms |     104.9361ms      |
| 104.8725ms | 104.1823ms |     115.2648ms      |
| 103.767ms  |     -      |     104.3818ms      |
| 127.6107ms |     -      |          -          |
| 113.268ms  |     -      |          -          |
                                                 
### Distribution:

                            ┌ ███████                                  (3)
    104.9762ms - 105.7012ms ├ ▒▒▒▒▒                                    (2)
                            └                                          (0)
                            ┌ ███████████████████                      (8)
    105.7012ms - 106.4262ms ├ ▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒                 (10)
                            └ ░░░░░░░░░░░░░░░░░░░                      (8)
                            ┌ █████████████████████                    (9)
    106.4262ms - 107.1512ms ├ ▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒          (13)
                            └ ░░░░░░░░░░░░░░░░░░░░░░░░                 (10)
                            ┌ ███████████████████████████████          (13)
    107.1512ms - 107.8762ms ├ ▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒      (15)
                            └ ░░░░░░░░░░░░░░░░                         (7)
                            ┌ ███████                                  (3)
    107.8762ms - 108.6012ms ├ ▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒                 (10)
                            └ ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░ (17)
                            ┌ ████████████████████████████             (12)
    108.6012ms - 109.3262ms ├ ▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒                    (9)
                            └ ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░          (13)
                            ┌ █████████████████████████████████        (14)
    109.3261ms - 110.0511ms ├ ▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒                    (9)
                            └ ░░░░░░░░░░░░░░░░░░░                      (8)
                            ┌ ██████████████                           (6)
    110.0511ms - 110.7761ms ├ ▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒                    (9)
                            └ ░░░░░░░░░░░░                             (5)
                            ┌ ████████████                             (5)
    110.7761ms - 111.5011ms ├ ▒▒▒▒▒▒▒▒▒▒▒▒                             (5)
                            └ ░░░░░░░░░░░░░░░░░░░                      (8)
                            ┌ █████████                                (4)
    111.5011ms - 112.2261ms ├ ▒▒▒▒▒▒▒▒▒                                (4)
                            └ ░░░░░░░░░░░░░░░░                         (7)
                            ┌ ███████████████████                      (8)
    112.2261ms - 112.9511ms ├ ▒▒▒▒▒                                    (2)
                            └ ░░░░░░░░░                                (4)
  Legend:
    █ : Callsite
    ▒ : CallTarget
    ░ : CallTarget+Inlining
  Range: 7.9749ms

### Summary:
                                                                                                                                                                                        
| Name                                        | Status | Mean             | StdDev      | StdErr     | Min        | Median     | Max        | P95        | P90              | Outliers |
| ------------------------------------------- | ------ | ---------------- | ----------- | ---------- | ---------- | ---------- | ---------- | ---------- | ---------------- | -------- |
| Callsite                                    | Passed | 108.9426ms       | 2.0188ms    | 0.2189ms   | 105.3984ms | 108.8787ms | 112.9511ms | 112.4665ms | 112.1536ms       | 15 {1.2} |
|   ├>process.corrected_duration_ms           |        | 73.82112         | 1.414598    | 0.169077   | 71.5705    | 73.76285   | 76.5742    | 76.36403   | 75.76946         | 13 {1.4} |
|   ├>process.internal_duration_ms            |        | 36.4032          | 0.165846    | 0.020414   | 36.1472    | 36.4544    | 36.6592    | 36.6592    | 36.6592          | 16 {1.2} |
|   ├>process.startuphook_overhead_ms         |        | 34.720614        | 0.160398    | 0.018773   | 34.5088    | 34.7136    | 35.0208    | 35.0208    | 34.9184          | 15 {1.1} |
|   ├>process.time_to_end_main_ms             |        | 3.698297         | 0.07774     | 0.009497   | 3.5914     | 3.6742     | 3.8552     | 3.83974    | 3.828047         | 15 {1.2} |
|   ├>process.time_to_end_ms                  |        | 3.616477         | 0.089045    | 0.010568   | 3.489      | 3.5921     | 3.8028     | 3.77897    | 3.75196          | 10 {1.4} |
|   ├>process.time_to_main_ms                 |        | 68.324333        | 1.415813    | 0.170444   | 66.0241    | 68.2349    | 71.1033    | 70.91762   | 70.23046         | 13 {1.4} |
|   ├>process.time_to_start_ms                |        | 33.582616        | 1.311821    | 0.160264   | 31.4907    | 33.3745    | 36.1885    | 35.89104   | 35.43982         | 14 {1.4} |
|   ├>runtime.dotnet.cpu.percent              |        | 6.601562         | 0.564962    | 0.057661   | 6.25       | 6.25       | 7.5        | 7.5        | 7.5              | 0        |
|   ├>runtime.dotnet.cpu.system               |        | 320.555556       | 24.739593   | 2.607782   | 300        | 300        | 350        | 350        | 350              | 0        |
|   ├>runtime.dotnet.cpu.user                 |        | 61.111111        | 20.90344    | 2.203416   | 50         | 50         | 100        | 100        | 100              | 0        |
|   ├>runtime.dotnet.mem.committed            |        | 143263217.371429 | 6548.085465 | 782.645908 | 143253504  | 143263744  | 143273984  | 143273984  | 143272482.133333 | 12 {1.2} |
|   ├>runtime.dotnet.threads.contention_count |        | 0                | 0           | 0          | 0          | 0          | 0          | 0          | 0                | 0        |
|   ├>runtime.dotnet.threads.contention_time  |        | 0                | 0           | 0          | 0          | 0          | 0          | 0          | 0                | 0        |
|   ├>runtime.dotnet.threads.count            |        | 9                | 0           | 0          | 9          | 9          | 9          | 9          | 9                | 0        |
|   ├>runtime.dotnet.threads.workers_count    |        | 0                | 0           | 0          | 0          | 0          | 0          | 0          | 0                | 0        |
|   ├>runtime.process.private_bytes           |        | 143263217.371429 | 6548.085465 | 782.645908 | 143253504  | 143263744  | 143273984  | 143273984  | 143272482.133333 | 12 {1.2} |
|   └>runtime.process.processor_time          |        | 264.0625         | 22.598469   | 2.306447   | 250        | 250        | 300        | 300        | 300              | 0        |
| CallTarget                                  | Passed | 108.4489ms       | 1.8471ms    | 0.1969ms   | 104.9762ms | 108.2143ms | 112.6558ms | 111.7741ms | 111.0873ms       | 12 {1.1} |
|   ├>process.corrected_duration_ms           |        | 73.494025        | 1.161826    | 0.14194    | 71.7469    | 73.402     | 75.6749    | 75.47155   | 75.14616         | 16 {1.3} |
|   ├>process.internal_duration_ms            |        | 36.372211        | 0.195023    | 0.022371   | 36.0448    | 36.352     | 36.7616    | 36.6592    | 36.6592          | 11 {1.3} |
|   ├>process.startuphook_overhead_ms         |        | 34.595109        | 0.131201    | 0.015681   | 34.4064    | 34.6112    | 34.816     | 34.816     | 34.7136          | 13 {1.4} |
|   ├>process.time_to_end_main_ms             |        | 3.695697         | 0.086281    | 0.010387   | 3.5681     | 3.6749     | 3.8596     | 3.85016    | 3.825787         | 17 {1.3} |
|   ├>process.time_to_end_ms                  |        | 3.596844         | 0.080558    | 0.009561   | 3.4814     | 3.5725     | 3.7643     | 3.74748    | 3.721727         | 15 {1.3} |
|   ├>process.time_to_main_ms                 |        | 67.849604        | 1.208974    | 0.14661    | 65.9691    | 67.767     | 70.1687    | 69.922225  | 69.70265         | 13 {1.4} |
|   ├>process.time_to_start_ms                |        | 33.298051        | 1.126381    | 0.137609   | 31.6508    | 33.2609    | 35.3202    | 35.20467   | 35.032587        | 14 {1.3} |
|   ├>runtime.dotnet.cpu.percent              |        | 6.25             | 0           | 0          | 6.25       | 6.25       | 6.25       | 6.25       | 6.25             | 0        |
|   ├>runtime.dotnet.cpu.system               |        | 313.297872       | 22.210542   | 2.290842   | 300        | 300        | 350        | 350        | 350              | 0        |
|   ├>runtime.dotnet.cpu.user                 |        | 50               | 0           | 0          | 50         | 50         | 50         | 50         | 50               | 12 {0.5} |
|   ├>runtime.dotnet.mem.committed            |        | 143288868.056338 | 7560.245241 | 897.236038 | 143278080  | 143290368  | 143302656  | 143302656  | 143300744.533333 | 15 {1}   |
|   ├>runtime.dotnet.threads.contention_count |        | 0                | 0           | 0          | 0          | 0          | 0          | 0          | 0                | 0        |
|   ├>runtime.dotnet.threads.contention_time  |        | 0                | 0           | 0          | 0          | 0          | 0          | 0          | 0                | 0        |
|   ├>runtime.dotnet.threads.count            |        | 9                | 0           | 0          | 9          | 9          | 9          | 9          | 9                | 0        |
|   ├>runtime.dotnet.threads.workers_count    |        | 0                | 0           | 0          | 0          | 0          | 0          | 0          | 0                | 0        |
|   ├>runtime.process.private_bytes           |        | 143288868.056338 | 7560.245241 | 897.236038 | 143278080  | 143290368  | 143302656  | 143302656  | 143300744.533333 | 15 {1}   |
|   └>runtime.process.processor_time          |        | 250              | 0           | 0          | 250        | 250        | 250        | 250        | 250              | 0        |
| CallTarget+Inlining                         | Passed | 108.9221ms       | 1.8819ms    | 0.2017ms   | 105.7922ms | 108.6976ms | 112.7802ms | 112.2111ms | 111.911ms        | 13 {1.1} |
|   ├>process.corrected_duration_ms           |        | 73.933967        | 1.256155    | 0.150139   | 71.5745    | 73.9554    | 76.4983    | 76.298555  | 75.799317        | 15 {1.5} |
|   ├>process.internal_duration_ms            |        | 36.436591        | 0.131367    | 0.015815   | 36.2496    | 36.4544    | 36.6592    | 36.6592    | 36.6592          | 16 {1.1} |
|   ├>process.startuphook_overhead_ms         |        | 34.709273        | 0.137859    | 0.016361   | 34.5088    | 34.7136    | 35.0208    | 35.0208    | 34.9184          | 17 {1.3} |
|   ├>process.time_to_end_main_ms             |        | 3.73053          | 0.077076    | 0.009212   | 3.602      | 3.72955    | 3.871      | 3.861505   | 3.847877         | 14 {1.4} |
|   ├>process.time_to_end_ms                  |        | 3.639668         | 0.07234     | 0.008773   | 3.5239     | 3.6374     | 3.7697     | 3.7608     | 3.749183         | 13 {1.4} |
|   ├>process.time_to_main_ms                 |        | 68.126586        | 1.235002    | 0.148677   | 65.9615    | 68.2915    | 70.9775    | 70.2568    | 69.71754         | 14 {1.5} |
|   ├>process.time_to_start_ms                |        | 33.494927        | 1.129705    | 0.138015   | 31.5109    | 33.7005    | 35.8348    | 35.16279   | 34.84454         | 15 {1.4} |
|   ├>runtime.dotnet.cpu.percent              |        | 6.581633         | 0.554707    | 0.056034   | 6.25       | 6.25       | 7.5        | 7.5        | 7.5              | 0        |
|   ├>runtime.dotnet.cpu.system               |        | 318.085106       | 24.153479   | 2.491241   | 300        | 300        | 350        | 350        | 350              | 0        |
|   ├>runtime.dotnet.cpu.user                 |        | 50               | 0           | 0          | 50         | 50         | 50         | 50         | 50               | 14 {0.5} |
|   ├>runtime.dotnet.mem.committed            |        | 143315867.042254 | 8313.890764 | 986.677307 | 143298560  | 143314944  | 143327232  | 143327232  | 143327232        | 12 {1.2} |
|   ├>runtime.dotnet.threads.contention_count |        | 0                | 0           | 0          | 0          | 0          | 0          | 0          | 0                | 0        |
|   ├>runtime.dotnet.threads.contention_time  |        | 0                | 0           | 0          | 0          | 0          | 0          | 0          | 0                | 0        |
|   ├>runtime.dotnet.threads.count            |        | 9                | 0           | 0          | 9          | 9          | 9          | 9          | 9                | 0        |
|   ├>runtime.dotnet.threads.workers_count    |        | 0                | 0           | 0          | 0          | 0          | 0          | 0          | 0                | 0        |
|   ├>runtime.process.private_bytes           |        | 143315867.042254 | 8313.890764 | 986.677307 | 143298560  | 143314944  | 143327232  | 143327232  | 143327232        | 12 {1.2} |
|   └>runtime.process.processor_time          |        | 263.265306       | 22.1883     | 2.241357   | 250        | 250        | 300        | 300        | 300              | 0        |
                                                                                                                                                                                        

### Overheads:
                                                                     
|                     | Callsite | CallTarget | CallTarget+Inlining |
| ------------------- | -------- | ---------- | ------------------- |
| Callsite            | --       | -0.5%      | --                  |
| CallTarget          | 0.5%     | --         | 0.4%                |
| CallTarget+Inlining | --       | -0.4%      | --                  |
                                                                     

The json file '/home/runner/work/timeitsharp/timeitsharp/src/TimeItSharp/bin/Release/net8.0/jsonexporter_2050655867.json' was exported.
The Datadog exported ran successfully.
The Datadog profiler was successfully attached to the .NET processes.
*** onFinish ***
ExecuteService.OnFinish: ProcessId: 6632, ProcessName: echo, Duration: 00:00:00.0007947, ExitCode: 0
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


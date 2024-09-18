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
ExecuteService.OnScenarioStart: ProcessId: 47054, ProcessName: echo, Duration: 00:00:00.0036770, ExitCode: 0
Scenario: Callsite
  Warming up ..........
    Duration: 1,236s
  Run ....................................................................................................
    Duration: 12,355s

*** onScenarioFinish ***
ExecuteService.OnScenarioFinish: ProcessId: 47389, ProcessName: echo, Duration: 00:00:00.0035740, ExitCode: 0
*** onScenarioStart ***
ExecuteService.OnScenarioStart: ProcessId: 47390, ProcessName: echo, Duration: 00:00:00.0021030, ExitCode: 0
Scenario: CallTarget
  Warming up ..........
    Duration: 1,277s
  Run ....................................................................................................
    Duration: 12,437s

*** onScenarioFinish ***
ExecuteService.OnScenarioFinish: ProcessId: 47728, ProcessName: echo, Duration: 00:00:00.0037410, ExitCode: 0
*** onScenarioStart ***
ExecuteService.OnScenarioStart: ProcessId: 47729, ProcessName: echo, Duration: 00:00:00.0024300, ExitCode: 0
Scenario: CallTarget+Inlining
  Warming up ..........
    Duration: 1,248s
  Run ....................................................................................................
    Duration: 12,451s

*** onScenarioFinish ***
ExecuteService.OnScenarioFinish: ProcessId: 48064, ProcessName: echo, Duration: 00:00:00.0022680, ExitCode: 0
*** afterAllScenariosFinishes ***
ExecuteService.AfterAllScenariosFinishes: ProcessId: 48065, ProcessName: echo, Duration: 00:00:00.0035830, ExitCode: 0
### Results:
                                               
| Callsite | CallTarget | CallTarget+Inlining |
| :------: | :--------: | :-----------------: |
| 72,891ms |  76,709ms  |      77,557ms       |
| 76,253ms |  74,808ms  |      75,643ms       |
| 75,939ms |  75,921ms  |       73,95ms       |
| 77,384ms |  76,045ms  |      74,165ms       |
| 74,351ms |  74,185ms  |      74,132ms       |
| 75,717ms |  74,577ms  |      74,884ms       |
| 76,472ms |  75,697ms  |      75,575ms       |
| 76,149ms |  75,603ms  |      75,287ms       |
| 73,192ms |  76,433ms  |      78,012ms       |
| 77,199ms |  75,099ms  |      77,894ms       |
| 76,272ms |  76,065ms  |      77,409ms       |
| 76,597ms |  76,037ms  |       76,25ms       |
| 73,476ms |  74,732ms  |      75,027ms       |
| 74,241ms |  76,353ms  |      74,974ms       |
| 77,363ms |  74,243ms  |      76,184ms       |
| 76,814ms |  74,664ms  |      76,995ms       |
| 75,245ms |  75,195ms  |      75,871ms       |
| 74,171ms |  74,378ms  |      75,836ms       |
| 75,645ms |  74,782ms  |       77,7ms        |
| 74,653ms |  74,348ms  |       75,28ms       |
| 76,977ms |   74,8ms   |      74,497ms       |
| 75,565ms |  75,571ms  |      75,091ms       |
| 77,599ms |  74,832ms  |      74,662ms       |
| 76,59ms  |  74,184ms  |      76,514ms       |
| 76,363ms |  74,645ms  |      75,351ms       |
| 75,913ms |  75,422ms  |      73,527ms       |
| 76,497ms |  75,682ms  |      75,782ms       |
| 75,61ms  |  74,418ms  |      75,195ms       |
| 74,006ms |  74,265ms  |      75,385ms       |
| 75,826ms |  75,506ms  |      74,958ms       |
| 76,038ms |  75,444ms  |      76,669ms       |
| 76,642ms |  75,454ms  |      75,141ms       |
| 75,522ms |  75,526ms  |      76,679ms       |
| 77,183ms |  76,323ms  |      77,168ms       |
| 74,625ms |  75,371ms  |      74,324ms       |
| 75,288ms |  74,963ms  |      75,526ms       |
| 75,892ms |  74,951ms  |      75,635ms       |
| 75,425ms |  75,285ms  |      75,508ms       |
| 74,439ms |  75,347ms  |       76,55ms       |
| 76,13ms  |  75,21ms   |      74,977ms       |
| 76,505ms |  75,856ms  |      76,557ms       |
| 75,718ms |  74,487ms  |       77,36ms       |
| 76,433ms |  75,942ms  |      76,594ms       |
| 75,908ms |  75,304ms  |       75,54ms       |
| 75,797ms |  74,902ms  |      75,732ms       |
| 76,431ms |  74,998ms  |      74,295ms       |
| 73,711ms |  75,09ms   |      75,503ms       |
| 76,443ms |  74,615ms  |      75,528ms       |
| 76,615ms |  75,721ms  |      75,932ms       |
| 77,635ms |  74,443ms  |      77,763ms       |
| 73,625ms |  74,293ms  |      77,703ms       |
| 75,756ms |  74,134ms  |      76,504ms       |
| 75,704ms |   74,3ms   |      75,725ms       |
| 74,57ms  |  73,665ms  |      76,265ms       |
| 74,632ms |  74,476ms  |      75,462ms       |
| 77,679ms |  74,367ms  |      76,236ms       |
| 74,545ms |  76,332ms  |      76,326ms       |
| 75,734ms |  76,295ms  |      75,901ms       |
| 75,677ms |  74,312ms  |      75,434ms       |
| 76,581ms |  75,489ms  |      75,514ms       |
| 77,977ms |  74,843ms  |      74,766ms       |
| 75,375ms |  74,474ms  |      73,614ms       |
| 74,101ms |  75,249ms  |      74,137ms       |
| 73,807ms |  75,408ms  |      73,796ms       |
| 73,106ms |  74,426ms  |      73,726ms       |
| 73,828ms |  74,273ms  |      73,921ms       |
| 72,587ms |  73,887ms  |      77,352ms       |
|  75,6ms  |  75,028ms  |      75,875ms       |
| 75,564ms |  75,519ms  |      74,129ms       |
| 73,686ms |  74,924ms  |      74,329ms       |
| 73,681ms |  74,899ms  |      74,048ms       |
| 74,504ms |  75,422ms  |       74,24ms       |
| 74,933ms |  74,592ms  |      73,267ms       |
| 75,706ms |  75,897ms  |      74,572ms       |
| 73,221ms |  75,13ms   |      75,097ms       |
| 75,206ms |  74,701ms  |      76,707ms       |
| 73,106ms |  74,93ms   |      73,735ms       |
| 75,331ms |  74,444ms  |      75,641ms       |
| 72,882ms |  73,58ms   |      75,531ms       |
| 73,324ms |  75,791ms  |       75,03ms       |
| 74,136ms |  74,569ms  |      76,853ms       |
| 75,557ms |  76,141ms  |      75,509ms       |
| 75,691ms |  75,224ms  |      74,395ms       |
| 75,088ms |     -      |       73,32ms       |
| 74,125ms |     -      |      75,005ms       |
| 74,551ms |     -      |      74,614ms       |
| 75,601ms |     -      |      75,868ms       |
| 75,003ms |     -      |      77,994ms       |
| 76,621ms |     -      |      76,618ms       |
| 74,015ms |     -      |      77,805ms       |
| 74,228ms |     -      |       75,29ms       |
| 73,575ms |     -      |      75,492ms       |
| 75,819ms |     -      |      76,556ms       |
| 74,025ms |     -      |      75,908ms       |
|    -     |     -      |          -          |
|    -     |     -      |          -          |
|    -     |     -      |          -          |
|    -     |     -      |          -          |
|    -     |     -      |          -          |
|    -     |     -      |          -          |
                                               
### Outliers:
                                               
| Callsite | CallTarget | CallTarget+Inlining |
| :------: | :--------: | :-----------------: |
|   78ms   |  77,748ms  |       80,44ms       |
| 78,272ms |  80,219ms  |      78,183ms       |
| 72,423ms |  77,152ms  |      73,237ms       |
| 72,29ms  |  73,494ms  |      79,428ms       |
| 72,192ms |  73,005ms  |      78,984ms       |
| 71,855ms |  78,366ms  |      72,714ms       |
|    -     |  78,169ms  |          -          |
|    -     |  79,515ms  |          -          |
|    -     |  77,961ms  |          -          |
|    -     |  73,347ms  |          -          |
|    -     |  78,544ms  |          -          |
|    -     |  77,952ms  |          -          |
|    -     |  73,182ms  |          -          |
|    -     |  73,397ms  |          -          |
|    -     |  73,241ms  |          -          |
|    -     |  73,567ms  |          -          |
|    -     |  77,084ms  |          -          |
                                               
### Distribution:

Callsite:
Scenario 'Callsite' is Bimodal. Peak count: 2
 72,5870ms - 73,1060ms: ██ (5)
 73,1920ms - 73,6250ms: ██ (6)
 73,6810ms - 74,1710ms: █████ (12)
 74,2280ms - 74,6530ms: █████ (11)
 74,9330ms - 75,2450ms: ██ (5)
 75,2880ms - 75,8190ms: ██████████ (22)
 75,8260ms - 76,2720ms: ████ (10)
 76,3630ms - 76,8140ms: ██████ (14)
 76,9770ms - 77,3840ms: ██ (5)
 77,5990ms - 77,9770ms: █ (4)

CallTarget:
 73,5800ms - 73,8870ms: █ (3)
 74,1340ms - 74,1850ms: █ (3)
 74,2430ms - 74,4870ms: ██████████ (16)
 74,5690ms - 74,8080ms: ██████ (11)
 74,8320ms - 75,1300ms: ████████ (13)
 75,1950ms - 75,4540ms: ████████ (13)
 75,4890ms - 75,7210ms: █████ (9)
 75,7910ms - 76,0650ms: █████ (8)
 76,1410ms - 76,3530ms: ███ (5)
 76,4330ms - 76,7090ms: █ (2)

CallTarget+Inlining:
 73,2670ms - 73,7350ms: ███ (6)
 73,7960ms - 74,1650ms: ████ (8)
 74,2400ms - 74,6620ms: ████ (9)
 74,7660ms - 75,1410ms: █████ (11)
 75,1950ms - 75,6350ms: ██████████ (19)
 75,6410ms - 75,9320ms: ██████ (12)
 76,1840ms - 76,5570ms: █████ (10)
 76,5940ms - 76,9950ms: ███ (7)
 77,1680ms - 77,4090ms: ██ (4)
 77,5570ms - 78,0120ms: ████ (8)

### Summary:
                                                                                                                                                                  
| Name                                        | Status | Mean       | StdDev   | StdErr   | Min      | Median    | Max      | P95        | P90        | Outliers |
| ------------------------------------------- | ------ | ---------- | -------- | -------- | -------- | --------- | -------- | ---------- | ---------- | -------- |
| Callsite                                    | Passed | 75,3129ms  | 1,2819ms | 0,1322ms | 72,587ms | 75,5825ms | 77,977ms | 77,3829ms  | 76,852ms   | 6 {2}    |
|   ├>process.corrected_duration_ms           |        | 60,560273  | 0,613604 | 0,075529 | 59,3686  | 60,5899   | 61,5546  | 61,45083   | 61,322273  | 15 {1,4} |
|   ├>process.internal_duration_ms            |        | 35,287661  | 0,364095 | 0,044817 | 34,6112  | 35,2768   | 35,9424  | 35,9424    | 35,84      | 15 {1,4} |
|   ├>process.startuphook_overhead_ms         |        | 14,766641  | 0,247295 | 0,028944 | 14,4384  | 14,7456   | 15,1552  | 15,1552    | 15,1552    | 10 {1,4} |
|   ├>process.time_to_end_main_ms             |        | 1,597885   | 0,077388 | 0,009526 | 1,479    | 1,6037    | 1,7322   | 1,71663    | 1,70482    | 16 {1,3} |
|   ├>process.time_to_end_ms                  |        | 1,576191   | 0,073486 | 0,009115 | 1,4726   | 1,556     | 1,697    | 1,68676    | 1,681453   | 16 {1,3} |
|   ├>process.time_to_main_ms                 |        | 38,345112  | 0,524167 | 0,063565 | 37,365   | 38,3944   | 39,2304  | 39,1361    | 39,013     | 13 {1,4} |
|   ├>process.time_to_start_ms                |        | 23,416321  | 0,385962 | 0,046805 | 22,7168  | 23,4026   | 24,1368  | 24,0303    | 23,9076    | 15 {1,4} |
|   ├>runtime.dotnet.cpu.percent              |        | 1,515625   | 0,023297 | 0,002378 | 1,5      | 1,5       | 1,55     | 1,55       | 1,55       | 0        |
|   ├>runtime.dotnet.cpu.system               |        | 224,36742  | 2,381561 | 0,286706 | 219,583  | 224,25    | 228,833  | 228,5751   | 227,8982   | 15 {1,5} |
|   ├>runtime.dotnet.cpu.user                 |        | 42,608258  | 1,648646 | 0,202934 | 39,896   | 42,6145   | 45,4795  | 45,1677    | 44,939583  | 16 {1,4} |
|   ├>runtime.dotnet.mem.committed            |        | 0          | 0        | 0        | 0        | 0         | 0        | 0          | 0          | 0        |
|   ├>runtime.dotnet.threads.contention_count |        | 0          | 0        | 0        | 0        | 0         | 0        | 0          | 0          | 0        |
|   ├>runtime.dotnet.threads.contention_time  |        | 0          | 0        | 0        | 0        | 0         | 0        | 0          | 0          | 0        |
|   ├>runtime.dotnet.threads.count            |        | 10         | 0        | 0        | 10       | 10        | 10       | 10         | 10         | 0        |
|   ├>runtime.dotnet.threads.workers_count    |        | 0          | 0        | 0        | 0        | 0         | 0        | 0          | 0          | 0        |
|   ├>runtime.process.private_bytes           |        | 0          | 0        | 0        | 0        | 0         | 0        | 0          | 0          | 0        |
|   └>runtime.process.processor_time          |        | 179,732224 | 1,666841 | 0,203637 | 177,479  | 179,271   | 183,4375 | 182,93525  | 182,640433 | 15 {1,3} |
| CallTarget                                  | Passed | 75,0776ms  | 0,6951ms | 0,0763ms | 73,58ms  | 74,998ms  | 76,709ms | 76,3275ms  | 76,0516ms  | 17 {1,3} |
|   ├>process.corrected_duration_ms           |        | 60,293567  | 0,418927 | 0,051566 | 59,6266  | 60,3119   | 61,189   | 60,98744   | 60,876793  | 16 {1,3} |
|   ├>process.internal_duration_ms            |        | 35,184334  | 0,317905 | 0,038838 | 34,6112  | 35,2256   | 35,84    | 35,7376    | 35,6352    | 14 {1,5} |
|   ├>process.startuphook_overhead_ms         |        | 14,686685  | 0,167147 | 0,019563 | 14,4384  | 14,7456   | 14,9504  | 14,9504    | 14,9504    | 9 {1,4}  |
|   ├>process.time_to_end_main_ms             |        | 1,563008   | 0,067998 | 0,008014 | 1,4416   | 1,5534    | 1,7164   | 1,69588    | 1,66396    | 12 {1,5} |
|   ├>process.time_to_end_ms                  |        | 1,535374   | 0,064836 | 0,007749 | 1,4274   | 1,5327    | 1,6708   | 1,65303    | 1,6391     | 15 {1,4} |
|   ├>process.time_to_main_ms                 |        | 38,146119  | 0,335164 | 0,040947 | 37,5314  | 38,1648   | 38,8076  | 38,67866   | 38,6124    | 16 {1,4} |
|   ├>process.time_to_start_ms                |        | 23,488945  | 0,293218 | 0,036369 | 22,9766  | 23,4782   | 24,0276  | 23,93272   | 23,8854    | 16 {1,4} |
|   ├>runtime.dotnet.cpu.percent              |        | 1,5        | 0        | 0        | 1,5      | 1,5       | 1,5      | 1,5        | 1,5        | 17 {0,5} |
|   ├>runtime.dotnet.cpu.system               |        | 222,939333 | 1,926344 | 0,231905 | 219,3335 | 222,979   | 226,229  | 225,8837   | 225,4165   | 14 {1,4} |
|   ├>runtime.dotnet.cpu.user                 |        | 43,3589    | 1,263955 | 0,151072 | 40,854   | 43,375    | 45,833   | 45,634375  | 45,037483  | 15 {1,5} |
|   ├>runtime.dotnet.mem.committed            |        | 0          | 0        | 0        | 0        | 0         | 0        | 0          | 0          | 0        |
|   ├>runtime.dotnet.threads.contention_count |        | 0          | 0        | 0        | 0        | 0         | 0        | 0          | 0          | 0        |
|   ├>runtime.dotnet.threads.contention_time  |        | 0          | 0        | 0        | 0        | 0         | 0        | 0          | 0          | 0        |
|   ├>runtime.dotnet.threads.count            |        | 10         | 0        | 0        | 10       | 10        | 10       | 10         | 10         | 0        |
|   ├>runtime.dotnet.threads.workers_count    |        | 0          | 0        | 0        | 0        | 0         | 0        | 0          | 0          | 0        |
|   ├>runtime.process.private_bytes           |        | 0          | 0        | 0        | 0        | 0         | 0        | 0          | 0          | 0        |
|   └>runtime.process.processor_time          |        | 178,859583 | 1,039621 | 0,127969 | 177,396  | 178,7395  | 181,0415 | 180,599075 | 180,418383 | 15 {1,2} |
| CallTarget+Inlining                         | Passed | 75,5784ms  | 1,1817ms | 0,1218ms | 73,267ms | 75,52ms   | 78,012ms | 77,76ms    | 77,3714ms  | 6 {1,7}  |
|   ├>process.corrected_duration_ms           |        | 60,605894  | 0,56281  | 0,069277 | 59,5912  | 60,5892   | 61,6676  | 61,56017   | 61,504713  | 16 {1,4} |
|   ├>process.internal_duration_ms            |        | 35,452121  | 0,343408 | 0,042271 | 34,9184  | 35,4304   | 36,0448  | 36,0448    | 35,9424    | 15 {1,3} |
|   ├>process.startuphook_overhead_ms         |        | 14,717305  | 0,207916 | 0,02385  | 14,4384  | 14,7456   | 15,0528  | 15,0528    | 15,0528    | 8 {1,4}  |
|   ├>process.time_to_end_main_ms             |        | 1,54493    | 0,089372 | 0,010606 | 1,4126   | 1,533     | 1,7194   | 1,7135     | 1,69412    | 11 {1,4} |
|   ├>process.time_to_end_ms                  |        | 1,525177   | 0,077887 | 0,009377 | 1,4034   | 1,5126    | 1,6922   | 1,6556     | 1,629173   | 13 {1,4} |
|   ├>process.time_to_main_ms                 |        | 38,385474  | 0,521914 | 0,062381 | 37,5636  | 38,3681   | 39,3852  | 39,32738   | 39,080607  | 15 {1,4} |
|   ├>process.time_to_start_ms                |        | 23,641736  | 0,395241 | 0,048651 | 23,011   | 23,6106   | 24,354   | 24,31303   | 24,27244   | 16 {1,3} |
|   ├>runtime.dotnet.cpu.percent              |        | 1,512887   | 0,021983 | 0,002232 | 1,5      | 1,5       | 1,55     | 1,55       | 1,55       | 0        |
|   ├>runtime.dotnet.cpu.system               |        | 224,903348 | 2,343735 | 0,288494 | 220,7915 | 224,73975 | 229,0005 | 228,695225 | 228,190067 | 16 {1,4} |
|   ├>runtime.dotnet.cpu.user                 |        | 44,392221  | 1,556824 | 0,186076 | 41,25    | 44,41675  | 47,021   | 46,884375  | 46,623083  | 13 {1,5} |
|   ├>runtime.dotnet.mem.committed            |        | 0          | 0        | 0        | 0        | 0         | 0        | 0          | 0          | 0        |
|   ├>runtime.dotnet.threads.contention_count |        | 0          | 0        | 0        | 0        | 0         | 0        | 0          | 0          | 0        |
|   ├>runtime.dotnet.threads.contention_time  |        | 0          | 0        | 0        | 0        | 0         | 0        | 0          | 0          | 0        |
|   ├>runtime.dotnet.threads.count            |        | 10         | 0        | 0        | 10       | 10        | 10       | 10         | 10         | 0        |
|   ├>runtime.dotnet.threads.workers_count    |        | 0          | 0        | 0        | 0        | 0         | 0        | 0          | 0          | 0        |
|   ├>runtime.process.private_bytes           |        | 0          | 0        | 0        | 0        | 0         | 0        | 0          | 0          | 0        |
|   └>runtime.process.processor_time          |        | 178,941992 | 1,170166 | 0,145141 | 177,3335 | 178,5625  | 181,8335 | 181,2457   | 180,924833 | 16 {1,2} |
                                                                                                                                                                  

### Overheads:
                                                                     
|                     | Callsite | CallTarget | CallTarget+Inlining |
| ------------------- | -------- | ---------- | ------------------- |
| Callsite            | --       | -0.3%      | 0.4%                |
| CallTarget          | 0.3%     | --         | 0.7%                |
| CallTarget+Inlining | -0.4%    | -0.7%      | --                  |
                                                                     

The json file '/Users/tony.redondo/repos/github/tonyredondo/timeitsharp/src/TimeItSharp/jsonexporter_1153325366.json' was exported.
The Datadog exported ran successfully.
The Datadog profiler could not be attached to the .NET processes.
*** onFinish ***
ExecuteService.OnFinish: ProcessId: 48068, ProcessName: echo, Duration: 00:00:00.0025620, ExitCode: 0


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


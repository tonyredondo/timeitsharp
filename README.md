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
TimeItSharp v0.1.15
Description:

Usage:
  TimeItSharp <configuration file or process name> [options]

Arguments:
  <configuration file or process name>  The JSON configuration file or process name

Options:
  --variable <variable>  Variables used to instantiate the configuration file [default: TimeItSharp.Common.TemplateVariables]
  --count <count>        Number of iterations to run
  --warmup <warmup>      Number of iterations to warm up
  --json-exporter        Enable JSON exporter [default: False]
  --datadog-exporter     Enable Datadog exporter [default: False]
  --version              Show version information
  -?, -h, --help         Show help and usage information
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
/Users/tony.redondo/repos/github/tonyredondo/timeitsharp/src/TimeItSharp/bin/Debug/net7.0/TimeItSharp config-example.json
Warmup count: 10
Count: 100
Number of Scenarios: 3
Exporters: ConsoleExporter, JsonExporter, Datadog
Assertors: DefaultAssertor
Services: NoopService

Scenario: Callsite
  Warming up ..........
    Duration: 1,2576003s
  Run ....................................................................................................
    Duration: 12,1142533s

Scenario: CallTarget
  Warming up ..........
    Duration: 1,2086785s
  Run ....................................................................................................
    Duration: 12,0948391s

Scenario: CallTarget & Inlining
  Warming up ..........
    Duration: 1,2076043s
  Run ....................................................................................................
    Duration: 12,0980161s

### Results:
                                                   
|  Callsite  | CallTarget | CallTarget & Inlining |
| :--------: | :--------: | :-------------------: |
| 113,9241ms | 113,7989ms |      113,9227ms       |
| 115,0473ms | 112,3052ms |      112,2312ms       |
| 115,6562ms | 113,8516ms |      113,8083ms       |
| 115,5837ms | 112,171ms  |      114,5854ms       |
| 114,531ms  | 113,1679ms |      113,7633ms       |
| 113,5719ms | 115,049ms  |      113,6634ms       |
| 112,996ms  | 112,2951ms |       114,808ms       |
| 113,9613ms | 114,2323ms |      114,1045ms       |
| 113,8192ms | 113,6768ms |      113,4776ms       |
| 115,2985ms | 115,3041ms |      113,5312ms       |
| 113,0053ms | 114,411ms  |      113,4308ms       |
| 113,1052ms | 115,1834ms |      115,1198ms       |
| 113,4218ms | 112,6477ms |      115,3083ms       |
| 113,6742ms | 112,769ms  |      115,1581ms       |
| 113,4795ms | 113,2891ms |      115,3983ms       |
| 113,5746ms | 113,1555ms |      113,7537ms       |
| 114,419ms  | 116,2608ms |      113,3428ms       |
| 115,0626ms | 114,5644ms |      114,4712ms       |
| 113,2105ms | 114,7242ms |      114,1649ms       |
| 114,1611ms | 114,5677ms |      114,3762ms       |
| 115,1171ms | 113,514ms  |      113,2202ms       |
| 116,5148ms | 113,9875ms |      112,7338ms       |
| 113,9275ms | 112,8656ms |      114,1884ms       |
| 114,0489ms | 113,3248ms |      114,9617ms       |
| 112,5197ms | 114,3962ms |      114,1384ms       |
| 116,0845ms | 113,7036ms |      114,5753ms       |
| 111,3858ms | 112,8948ms |      114,0322ms       |
| 113,6367ms | 111,6799ms |      114,6525ms       |
| 114,3547ms | 112,4421ms |      114,8146ms       |
| 112,6465ms | 112,1149ms |      114,9683ms       |
| 113,6054ms | 113,0083ms |      114,4887ms       |
| 113,8139ms | 112,8158ms |      115,1757ms       |
| 114,2592ms | 114,0464ms |      113,5708ms       |
| 111,2637ms | 112,7513ms |      114,0004ms       |
| 113,8222ms | 113,8165ms |      114,1322ms       |
| 114,7835ms | 112,9097ms |      113,4548ms       |
| 114,8584ms | 114,4416ms |      115,6075ms       |
| 115,1535ms | 112,3971ms |       112,824ms       |
| 113,4796ms | 113,6698ms |      112,7386ms       |
| 113,824ms  | 114,1509ms |      113,8929ms       |
| 114,2785ms | 114,0041ms |      113,7942ms       |
| 112,6768ms | 114,0956ms |      114,5547ms       |
| 115,0067ms | 113,2544ms |      114,1003ms       |
| 114,0862ms | 113,2479ms |       113,338ms       |
| 115,8104ms | 113,3179ms |      113,9478ms       |
| 113,9974ms | 113,7976ms |      115,0846ms       |
| 113,6279ms | 114,0461ms |      113,9872ms       |
| 113,7268ms | 114,4815ms |      112,2872ms       |
| 112,8952ms | 114,363ms  |      114,3088ms       |
| 114,4298ms | 113,8841ms |      114,2478ms       |
| 111,8807ms | 112,6619ms |      113,8354ms       |
| 113,721ms  | 113,5198ms |       113,427ms       |
| 115,1939ms | 113,5069ms |      112,8888ms       |
| 113,5806ms | 112,1734ms |       113,712ms       |
| 114,9851ms | 113,2249ms |      114,1256ms       |
| 112,4978ms | 114,8577ms |      114,6791ms       |
| 113,8068ms | 114,7471ms |       114,014ms       |
| 113,845ms  | 114,7619ms |       113,45ms        |
| 114,4426ms | 115,2678ms |      113,3019ms       |
| 111,5579ms | 115,0766ms |      112,6483ms       |
| 115,1886ms | 113,3607ms |      114,5278ms       |
| 114,3367ms | 115,1138ms |      114,0671ms       |
| 114,5135ms | 114,2602ms |       113,213ms       |
| 112,918ms  | 112,7266ms |      113,2041ms       |
| 113,2955ms | 113,5682ms |      112,6389ms       |
| 113,8396ms | 114,4808ms |      113,1806ms       |
| 113,8145ms | 114,8987ms |      112,0416ms       |
| 113,0066ms | 113,2022ms |       113,288ms       |
| 113,528ms  | 112,5776ms |      113,0443ms       |
| 113,9804ms | 114,5326ms |      113,3947ms       |
| 112,5082ms | 113,9755ms |      113,9503ms       |
| 113,8093ms | 114,0297ms |      114,4926ms       |
| 113,7797ms | 113,1294ms |      113,8655ms       |
| 113,8824ms | 112,8525ms |      113,8925ms       |
| 113,8264ms | 113,9562ms |      114,2753ms       |
| 113,1531ms | 115,2666ms |      114,8472ms       |
| 113,401ms  | 115,7456ms |      112,5652ms       |
| 114,1964ms | 112,3506ms |      114,3756ms       |
| 114,3784ms | 114,7119ms |      113,6793ms       |
| 113,3102ms | 112,5881ms |      113,5835ms       |
| 114,0532ms | 112,7473ms |      113,6977ms       |
| 114,6785ms | 112,0544ms |      112,1273ms       |
| 114,9231ms | 114,8453ms |      113,0149ms       |
| 113,4759ms | 113,901ms  |      113,5979ms       |
| 114,0546ms | 112,7365ms |      112,4997ms       |
| 116,7696ms | 114,8267ms |      113,9719ms       |
|  116,12ms  | 114,3021ms |      114,5265ms       |
| 113,9594ms | 116,5193ms |      114,4538ms       |
| 113,3661ms | 113,2813ms |      112,9942ms       |
| 112,4776ms | 113,8068ms |      112,6043ms       |
| 113,5599ms | 114,4247ms |      114,8679ms       |
| 113,8415ms | 113,0668ms |      113,4885ms       |
| 113,8218ms | 116,3874ms |      114,4866ms       |
| 113,5723ms | 115,7392ms |      112,8485ms       |
| 114,2455ms |     -      |      113,4365ms       |
| 114,0397ms |     -      |      114,8336ms       |
| 113,6698ms |     -      |      113,2389ms       |
| 115,8772ms |     -      |      114,9704ms       |
| 112,0637ms |     -      |           -           |
|     -      |     -      |           -           |

### Outliers:

|  Callsite  | CallTarget | CallTarget & Inlining |
| :--------: | :--------: | :-------------------: |
| 124,0938ms | 109,1599ms |      120,0334ms       |
|     -      | 109,5055ms |      116,2709ms       |
|     -      | 117,8375ms |           -           |
|     -      | 119,3301ms |           -           |
|     -      | 116,8417ms |           -           |
|     -      | 117,8582ms |           -           |

### Summary:

| Name                                        | Status | Mean           | StdDev       | StdErr      | Min        | Max        | P95        | P90            | Outliers |
| ------------------------------------------- | ------ | -------------- | ------------ | ----------- | ---------- | ---------- | ---------- | -------------- | -------- |
| Callsite                                    | Passed | 113,9382ms     | 1,0308ms     | 0,1036ms    | 111,2637ms | 116,7696ms | 115,8571ms | 115,1924ms     | 1        |
|   ├>process.corrected_duration_ms           |        | 80,749194      | 0,730394     | 0,07416     | 79,1127    | 83,08      | 81,8927    | 81,60264       | 1        |
|   ├>process.internal_duration_ms            |        | 41,639184      | 0,532063     | 0,053746    | 40,5504    | 43,3152    | 42,5472    | 42,3936        | 1        |
|   ├>process.startuphook_overhead_ms         |        | 33,17969       | 0,559127     | 0,05648     | 31,744     | 34,7136    | 34,176     | 33,8944        | 0        |
|   ├>process.time_to_end_main_ms             |        | 4,437049       | 0,133321     | 0,013399    | 4,1651     | 4,7962     | 4,71259    | 4,626          | 0        |
|   ├>process.time_to_end_ms                  |        | 4,377058       | 0,123479     | 0,01241     | 4,1645     | 4,7368     | 4,63027    | 4,55234        | 0        |
|   ├>process.time_to_main_ms                 |        | 67,843007      | 0,92528      | 0,092994    | 65,0737    | 70,3478    | 69,86138   | 69,149653      | 0        |
|   ├>process.time_to_start_ms                |        | 34,69665       | 0,484168     | 0,049415    | 33,8002    | 36,275     | 35,621665  | 35,380203      | 2        |
|   ├>runtime.dotnet.cpu.percent              |        | 0,07483        | 0,182453     | 0,01843     | 0          | 0,666667   | 0,666667   | 0,333333       | N/A      |
|   ├>runtime.dotnet.cpu.system               |        | 30,241935      | 62,538947    | 6,484987    | 0          | 208,333333 | 208,333333 | 104,166667     | 4        |
|   ├>runtime.dotnet.cpu.user                 |        | 15,190972      | 36,957475    | 3,771956    | 0          | 104,166667 | 104,166667 | 104,166667     | 0        |
|   ├>runtime.dotnet.exceptions.count         |        | 1              | 0            | 0           | 1          | 1          | 1          | 1              | 0        |
|   ├>runtime.dotnet.mem.committed            |        | 6554548,547368 | 32453,036804 | 3329,611322 | 6492160    | 6656000    | 6643302,4  | 6578722,133333 | 3        |
|   ├>runtime.dotnet.threads.contention_count |        | 0              | 0            | 0           | 0          | 0          | 0          | 0              | 0        |
|   ├>runtime.dotnet.threads.contention_time  |        | 0              | 0            | 0           | 0          | 0          | 0          | 0              | 0        |
|   ├>runtime.dotnet.threads.count            |        | 10             | 0            | 0           | 10         | 10         | 10         | 10             | 0        |
|   ├>runtime.dotnet.threads.workers_count    |        | 1,333333       | 0            | 0           | 1,333333   | 1,333333   | 1,333333   | 1,333333       | 0        |
|   ├>runtime.process.private_bytes           |        | 6554548,547368 | 32453,036804 | 3329,611322 | 6492160    | 6656000    | 6643302,4  | 6578722,133333 | 3        |
|   └>runtime.process.processor_time          |        | 23,384354      | 57,016408    | 5,759527    | 0          | 208,333333 | 208,333333 | 104,166667     | N/A      |
| CallTarget                                  | Passed | 113,7937ms     | 1,045ms      | 0,1077ms    | 111,6799ms | 116,5193ms | 115,7174ms | 115,13ms       | 6        |
|   ├>process.corrected_duration_ms           |        | 80,641606      | 0,794299     | 0,081068    | 78,9504    | 82,9776    | 81,9072    | 81,6632        | 2        |
|   ├>process.internal_duration_ms            |        | 41,664388      | 0,606561     | 0,060962    | 40,3456    | 43,3152    | 42,67008   | 42,468693      | 0        |
|   ├>process.startuphook_overhead_ms         |        | 33,113204      | 0,548525     | 0,055694    | 31,6416    | 34,6112    | 34,18112   | 33,8944        | 1        |
|   ├>process.time_to_end_main_ms             |        | 4,402017       | 0,163019     | 0,016384    | 4,0581     | 4,8713     | 4,74183    | 4,648973       | 0        |
|   ├>process.time_to_end_ms                  |        | 4,347197       | 0,149074     | 0,014982    | 4,0581     | 4,7689     | 4,66037    | 4,552587       | 0        |
|   ├>process.time_to_main_ms                 |        | 67,794415      | 0,834686     | 0,08475     | 66,3773    | 70,1089    | 69,40198   | 68,923013      | 0        |
|   ├>process.time_to_start_ms                |        | 34,679836      | 0,459767     | 0,046682    | 33,8467    | 36,0204    | 35,54448   | 35,3139        | 1        |
|   ├>runtime.dotnet.cpu.percent              |        | 0,21           | 0,278907     | 0,027891    | 0          | 1          | 0,666667   | 0,666667       | 0        |
|   ├>runtime.dotnet.cpu.system               |        | 109,375        | 123,318339   | 12,331834   | 0          | 416,666667 | 380,208333 | 312,5          | 0        |
|   ├>runtime.dotnet.cpu.user                 |        | 41,035354      | 62,814863    | 6,313131    | 0          | 208,333333 | 208,333333 | 104,166667     | 0        |
|   ├>runtime.dotnet.exceptions.count         |        | 1              | 0            | 0           | 1          | 1          | 1          | 1              | 0        |
|   ├>runtime.dotnet.mem.committed            |        | 6554732,93617  | 24600,976251 | 2537,396958 | 6504448    | 6639616    | 6598656    | 6586368        | 3        |
|   ├>runtime.dotnet.threads.contention_count |        | 0              | 0            | 0           | 0          | 0          | 0          | 0              | 0        |
|   ├>runtime.dotnet.threads.contention_time  |        | 0              | 0            | 0           | 0          | 0          | 0          | 0              | 0        |
|   ├>runtime.dotnet.threads.count            |        | 10             | 0            | 0           | 10         | 10         | 10         | 10             | 0        |
|   ├>runtime.dotnet.threads.workers_count    |        | 1,333333       | 0            | 0           | 1,333333   | 1,333333   | 1,333333   | 1,333333       | 0        |
|   ├>runtime.process.private_bytes           |        | 6554732,93617  | 24600,976251 | 2537,396958 | 6504448    | 6639616    | 6598656    | 6586368        | 3        |
|   └>runtime.process.processor_time          |        | 65,625         | 87,158374    | 8,715837    | 0          | 312,5      | 208,333333 | 208,333333     | 0        |
| CallTarget & Inlining                       | Passed | 113,8582ms     | 0,8006ms     | 0,0808ms    | 112,0416ms | 115,6075ms | 115,1485ms | 114,946ms      | 2        |
|   ├>process.corrected_duration_ms           |        | 80,631593      | 0,688826     | 0,06923     | 79,0521    | 82,4299    | 81,69873   | 81,55296       | 0        |
|   ├>process.internal_duration_ms            |        | 41,648128      | 0,529608     | 0,052961    | 40,5504    | 43,008     | 42,52672   | 42,3936        | 0        |
|   ├>process.startuphook_overhead_ms         |        | 33,222531      | 0,493414     | 0,049842    | 32,0512    | 34,4064    | 33,9968    | 33,792         | 0        |
|   ├>process.time_to_end_main_ms             |        | 4,385025       | 0,148015     | 0,014801    | 4,0568     | 4,7748     | 4,679905   | 4,62272        | 0        |
|   ├>process.time_to_end_ms                  |        | 4,319489       | 0,148255     | 0,014826    | 4,0568     | 4,7502     | 4,577505   | 4,53148        | 0        |
|   ├>process.time_to_main_ms                 |        | 67,849175      | 0,611857     | 0,061494    | 66,7709    | 69,5804    | 68,79788   | 68,6341        | 0        |
|   ├>process.time_to_start_ms                |        | 34,677763      | 0,412835     | 0,041703    | 33,7518    | 35,6989    | 35,470225  | 35,244983      | 1        |
|   ├>runtime.dotnet.cpu.percent              |        | 0,255892       | 0,311635     | 0,03132     | 0          | 1          | 1          | 0,666667       | 0        |
|   ├>runtime.dotnet.cpu.system               |        | 107,323232     | 120,392833   | 12,099935   | 0          | 416,666667 | 312,5      | 312,5          | 0        |
|   ├>runtime.dotnet.cpu.user                 |        | 20,616319      | 41,720885    | 4,25812     | 0          | 104,166667 | 104,166667 | 104,166667     | 0        |
|   ├>runtime.dotnet.exceptions.count         |        | 1              | 0            | 0           | 1          | 1          | 1          | 1              | 0        |
|   ├>runtime.dotnet.mem.committed            |        | 6556432,340426 | 25163,747726 | 2595,442404 | 6500352    | 6631424    | 6610329,6  | 6583227,733333 | 3        |
|   ├>runtime.dotnet.threads.contention_count |        | 0              | 0            | 0           | 0          | 0          | 0          | 0              | 0        |
|   ├>runtime.dotnet.threads.contention_time  |        | 0              | 0            | 0           | 0          | 0          | 0          | 0              | 0        |
|   ├>runtime.dotnet.threads.count            |        | 10             | 0            | 0           | 10         | 10         | 10         | 10             | 0        |
|   ├>runtime.dotnet.threads.workers_count    |        | 1,333333       | 0            | 0           | 1,333333   | 1,333333   | 1,333333   | 1,333333       | 0        |
|   ├>runtime.process.private_bytes           |        | 6556432,340426 | 25163,747726 | 2595,442404 | 6500352    | 6631424    | 6610329,6  | 6583227,733333 | 3        |
|   └>runtime.process.processor_time          |        | 79,96633       | 97,385895    | 9,787651    | 0          | 312,5      | 312,5      | 208,333333     | 0        |


The json file 'C:\github\tonyredondo\timeitsharp\test\TimeItSharp.FluentConfiguration.Sample\bin\Debug\net7.0\jsonexporter_539156412.json' was exported.
The Datadog exported ran successfully.

```

## Output is markdown compatible

Example:

### Results:

|  Callsite  | CallTarget | CallTarget & Inlining |
| :--------: | :--------: | :-------------------: |
| 113,9241ms | 113,7989ms |      113,9227ms       |
| 115,0473ms | 112,3052ms |      112,2312ms       |
| 115,6562ms | 113,8516ms |      113,8083ms       |
| 115,5837ms | 112,171ms  |      114,5854ms       |
| 114,531ms  | 113,1679ms |      113,7633ms       |
| 113,5719ms | 115,049ms  |      113,6634ms       |
| 112,996ms  | 112,2951ms |       114,808ms       |
...

### Outliers:

|  Callsite  | CallTarget | CallTarget & Inlining |
| :--------: | :--------: | :-------------------: |
| 124,0938ms | 109,1599ms |      120,0334ms       |
|     -      | 109,5055ms |      116,2709ms       |
|     -      | 117,8375ms |           -           |
|     -      | 119,3301ms |           -           |
|     -      | 116,8417ms |           -           |
|     -      | 117,8582ms |           -           |

### Summary:

| Name                                        | Status | Mean           | StdDev       | StdErr      | Min        | Max        | P95        | P90            | Outliers |
| ------------------------------------------- | ------ | -------------- | ------------ | ----------- | ---------- | ---------- | ---------- | -------------- | -------- |
| Callsite                                    | Passed | 113,9382ms     | 1,0308ms     | 0,1036ms    | 111,2637ms | 116,7696ms | 115,8571ms | 115,1924ms     | 1        |
|   ├>process.corrected_duration_ms           |        | 80,749194      | 0,730394     | 0,07416     | 79,1127    | 83,08      | 81,8927    | 81,60264       | 1        |
|   ├>process.internal_duration_ms            |        | 41,639184      | 0,532063     | 0,053746    | 40,5504    | 43,3152    | 42,5472    | 42,3936        | 1        |
|   ├>process.startuphook_overhead_ms         |        | 33,17969       | 0,559127     | 0,05648     | 31,744     | 34,7136    | 34,176     | 33,8944        | 0        |
|   ├>process.time_to_end_main_ms             |        | 4,437049       | 0,133321     | 0,013399    | 4,1651     | 4,7962     | 4,71259    | 4,626          | 0        |
|   ├>process.time_to_end_ms                  |        | 4,377058       | 0,123479     | 0,01241     | 4,1645     | 4,7368     | 4,63027    | 4,55234        | 0        |
|   ├>process.time_to_main_ms                 |        | 67,843007      | 0,92528      | 0,092994    | 65,0737    | 70,3478    | 69,86138   | 69,149653      | 0        |
|   ├>process.time_to_start_ms                |        | 34,69665       | 0,484168     | 0,049415    | 33,8002    | 36,275     | 35,621665  | 35,380203      | 2        |
|   ├>runtime.dotnet.cpu.percent              |        | 0,07483        | 0,182453     | 0,01843     | 0          | 0,666667   | 0,666667   | 0,333333       | N/A      |
|   ├>runtime.dotnet.cpu.system               |        | 30,241935      | 62,538947    | 6,484987    | 0          | 208,333333 | 208,333333 | 104,166667     | 4        |
|   ├>runtime.dotnet.cpu.user                 |        | 15,190972      | 36,957475    | 3,771956    | 0          | 104,166667 | 104,166667 | 104,166667     | 0        |
|   ├>runtime.dotnet.exceptions.count         |        | 1              | 0            | 0           | 1          | 1          | 1          | 1              | 0        |
|   ├>runtime.dotnet.mem.committed            |        | 6554548,547368 | 32453,036804 | 3329,611322 | 6492160    | 6656000    | 6643302,4  | 6578722,133333 | 3        |
|   ├>runtime.dotnet.threads.contention_count |        | 0              | 0            | 0           | 0          | 0          | 0          | 0              | 0        |
|   ├>runtime.dotnet.threads.contention_time  |        | 0              | 0            | 0           | 0          | 0          | 0          | 0              | 0        |
|   ├>runtime.dotnet.threads.count            |        | 10             | 0            | 0           | 10         | 10         | 10         | 10             | 0        |
|   ├>runtime.dotnet.threads.workers_count    |        | 1,333333       | 0            | 0           | 1,333333   | 1,333333   | 1,333333   | 1,333333       | 0        |
|   ├>runtime.process.private_bytes           |        | 6554548,547368 | 32453,036804 | 3329,611322 | 6492160    | 6656000    | 6643302,4  | 6578722,133333 | 3        |
|   └>runtime.process.processor_time          |        | 23,384354      | 57,016408    | 5,759527    | 0          | 208,333333 | 208,333333 | 104,166667     | N/A      |
| CallTarget                                  | Passed | 113,7937ms     | 1,045ms      | 0,1077ms    | 111,6799ms | 116,5193ms | 115,7174ms | 115,13ms       | 6        |
|   ├>process.corrected_duration_ms           |        | 80,641606      | 0,794299     | 0,081068    | 78,9504    | 82,9776    | 81,9072    | 81,6632        | 2        |
|   ├>process.internal_duration_ms            |        | 41,664388      | 0,606561     | 0,060962    | 40,3456    | 43,3152    | 42,67008   | 42,468693      | 0        |
|   ├>process.startuphook_overhead_ms         |        | 33,113204      | 0,548525     | 0,055694    | 31,6416    | 34,6112    | 34,18112   | 33,8944        | 1        |
|   ├>process.time_to_end_main_ms             |        | 4,402017       | 0,163019     | 0,016384    | 4,0581     | 4,8713     | 4,74183    | 4,648973       | 0        |
|   ├>process.time_to_end_ms                  |        | 4,347197       | 0,149074     | 0,014982    | 4,0581     | 4,7689     | 4,66037    | 4,552587       | 0        |
|   ├>process.time_to_main_ms                 |        | 67,794415      | 0,834686     | 0,08475     | 66,3773    | 70,1089    | 69,40198   | 68,923013      | 0        |
|   ├>process.time_to_start_ms                |        | 34,679836      | 0,459767     | 0,046682    | 33,8467    | 36,0204    | 35,54448   | 35,3139        | 1        |
|   ├>runtime.dotnet.cpu.percent              |        | 0,21           | 0,278907     | 0,027891    | 0          | 1          | 0,666667   | 0,666667       | 0        |
|   ├>runtime.dotnet.cpu.system               |        | 109,375        | 123,318339   | 12,331834   | 0          | 416,666667 | 380,208333 | 312,5          | 0        |
|   ├>runtime.dotnet.cpu.user                 |        | 41,035354      | 62,814863    | 6,313131    | 0          | 208,333333 | 208,333333 | 104,166667     | 0        |
|   ├>runtime.dotnet.exceptions.count         |        | 1              | 0            | 0           | 1          | 1          | 1          | 1              | 0        |
|   ├>runtime.dotnet.mem.committed            |        | 6554732,93617  | 24600,976251 | 2537,396958 | 6504448    | 6639616    | 6598656    | 6586368        | 3        |
|   ├>runtime.dotnet.threads.contention_count |        | 0              | 0            | 0           | 0          | 0          | 0          | 0              | 0        |
|   ├>runtime.dotnet.threads.contention_time  |        | 0              | 0            | 0           | 0          | 0          | 0          | 0              | 0        |
|   ├>runtime.dotnet.threads.count            |        | 10             | 0            | 0           | 10         | 10         | 10         | 10             | 0        |
|   ├>runtime.dotnet.threads.workers_count    |        | 1,333333       | 0            | 0           | 1,333333   | 1,333333   | 1,333333   | 1,333333       | 0        |
|   ├>runtime.process.private_bytes           |        | 6554732,93617  | 24600,976251 | 2537,396958 | 6504448    | 6639616    | 6598656    | 6586368        | 3        |
|   └>runtime.process.processor_time          |        | 65,625         | 87,158374    | 8,715837    | 0          | 312,5      | 208,333333 | 208,333333     | 0        |
| CallTarget & Inlining                       | Passed | 113,8582ms     | 0,8006ms     | 0,0808ms    | 112,0416ms | 115,6075ms | 115,1485ms | 114,946ms      | 2        |
|   ├>process.corrected_duration_ms           |        | 80,631593      | 0,688826     | 0,06923     | 79,0521    | 82,4299    | 81,69873   | 81,55296       | 0        |
|   ├>process.internal_duration_ms            |        | 41,648128      | 0,529608     | 0,052961    | 40,5504    | 43,008     | 42,52672   | 42,3936        | 0        |
|   ├>process.startuphook_overhead_ms         |        | 33,222531      | 0,493414     | 0,049842    | 32,0512    | 34,4064    | 33,9968    | 33,792         | 0        |
|   ├>process.time_to_end_main_ms             |        | 4,385025       | 0,148015     | 0,014801    | 4,0568     | 4,7748     | 4,679905   | 4,62272        | 0        |
|   ├>process.time_to_end_ms                  |        | 4,319489       | 0,148255     | 0,014826    | 4,0568     | 4,7502     | 4,577505   | 4,53148        | 0        |
|   ├>process.time_to_main_ms                 |        | 67,849175      | 0,611857     | 0,061494    | 66,7709    | 69,5804    | 68,79788   | 68,6341        | 0        |
|   ├>process.time_to_start_ms                |        | 34,677763      | 0,412835     | 0,041703    | 33,7518    | 35,6989    | 35,470225  | 35,244983      | 1        |
|   ├>runtime.dotnet.cpu.percent              |        | 0,255892       | 0,311635     | 0,03132     | 0          | 1          | 1          | 0,666667       | 0        |
|   ├>runtime.dotnet.cpu.system               |        | 107,323232     | 120,392833   | 12,099935   | 0          | 416,666667 | 312,5      | 312,5          | 0        |
|   ├>runtime.dotnet.cpu.user                 |        | 20,616319      | 41,720885    | 4,25812     | 0          | 104,166667 | 104,166667 | 104,166667     | 0        |
|   ├>runtime.dotnet.exceptions.count         |        | 1              | 0            | 0           | 1          | 1          | 1          | 1              | 0        |
|   ├>runtime.dotnet.mem.committed            |        | 6556432,340426 | 25163,747726 | 2595,442404 | 6500352    | 6631424    | 6610329,6  | 6583227,733333 | 3        |
|   ├>runtime.dotnet.threads.contention_count |        | 0              | 0            | 0           | 0          | 0          | 0          | 0              | 0        |
|   ├>runtime.dotnet.threads.contention_time  |        | 0              | 0            | 0           | 0          | 0          | 0          | 0              | 0        |
|   ├>runtime.dotnet.threads.count            |        | 10             | 0            | 0           | 10         | 10         | 10         | 10             | 0        |
|   ├>runtime.dotnet.threads.workers_count    |        | 1,333333       | 0            | 0           | 1,333333   | 1,333333   | 1,333333   | 1,333333       | 0        |
|   ├>runtime.process.private_bytes           |        | 6556432,340426 | 25163,747726 | 2595,442404 | 6500352    | 6631424    | 6610329,6  | 6583227,733333 | 3        |
|   └>runtime.process.processor_time          |        | 79,96633       | 97,385895    | 9,787651    | 0          | 312,5      | 312,5      | 208,333333     | 0        |


## Datadog Exporter:

The datadog exporter send all the data using the CI Test Visibility public api:

### Benchmark data:
<img width="1519" alt="image" src="https://user-images.githubusercontent.com/69803/223069595-c6531c45-2085-4fbc-8d4f-79854c0ca58d.png">

### Metrics from the startup hook:
<img width="818" alt="image" src="https://user-images.githubusercontent.com/69803/223069816-c3caf562-1cd2-46d3-8803-f42c6679647e.png">

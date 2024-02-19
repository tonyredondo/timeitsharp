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
TimeItSharp v0.1.17
Description:

Usage:
  TimeItSharp <configuration file or process name> [options]

Arguments:
  <configuration file or process name>  The JSON configuration file or process name

Options:
  --variable <variable>  Variables used to instantiate the configuration file [default: TimeItSharp.Common.TemplateVariables]
  --count <count>        Number of iterations to run
  --warmup <warmup>      Number of iterations to warm up
  --metrics              Enable Metrics from startup hook [default: True]
  --json-exporter        Enable JSON exporter [default: False]
  --datadog-exporter     Enable Datadog exporter [default: False]
  --datadog-profiler     Enable Datadog profiler [default: False]
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
    },
    {
      "name": "DatadogProfiler",
      "options": {
        "useExtraRun": true,
        "extraRunCount": 5,
        "scenarios" : [ "CallTarget" ]
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

TimeItSharp v0.1.17
Warmup count: 10
Count: 100
Number of Scenarios: 3
Exporters: ConsoleExporter, JsonExporter, Datadog
Assertors: DefaultAssertor
Services: NoopService, DatadogProfiler

Scenario: Callsite
  Warming up ..........
    Duration: 1,337s
  Run ....................................................................................................
    Duration: 12,897s

Scenario: CallTarget
  Warming up ..........
    Duration: 1,292s
  Run ....................................................................................................
    Duration: 13,419s
  Run for 'DatadogProfiler' .....
    Duration: 9,55s

Scenario: CallTarget+Inlining
  Warming up ..........
    Duration: 1,336s
  Run ....................................................................................................
    Duration: 13,224s

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
| 119,224ms  | 117,6297ms |     119,8465ms      |
| 119,7619ms | 119,2179ms |     120,3251ms      |
| 117,9631ms | 119,2626ms |     119,0609ms      |
| 119,2305ms | 118,9161ms |     118,7126ms      |
| 118,3015ms | 117,7652ms |     117,9345ms      |
| 118,4631ms | 120,4009ms |     120,9469ms      |
| 119,8335ms | 118,126ms  |     118,2422ms      |
| 119,5295ms | 126,859ms  |     118,1845ms      |
| 120,4548ms | 125,5516ms |     119,3985ms      |
| 118,5036ms | 126,5251ms |     119,5762ms      |
| 117,8923ms | 125,3918ms |     117,4158ms      |
| 119,0952ms | 126,4343ms |     116,6634ms      |
| 120,2239ms | 125,224ms  |     117,2367ms      |
| 117,9418ms | 125,8132ms |     119,8465ms      |
| 118,2575ms | 123,086ms  |     119,2945ms      |
| 119,4244ms | 124,0149ms |     118,7745ms      |
| 118,0589ms | 120,3965ms |     119,1586ms      |
| 119,0948ms | 122,0228ms |     119,7424ms      |
| 118,6426ms | 121,3467ms |     118,7103ms      |
| 119,5858ms | 121,1876ms |     119,3002ms      |
| 118,7724ms | 120,5666ms |     119,1506ms      |
| 118,2911ms | 122,2227ms |     120,8199ms      |
| 117,8955ms | 120,6929ms |     120,9141ms      |
| 117,5719ms |  121,68ms  |      120,185ms      |
| 117,634ms  | 119,4997ms |     119,4104ms      |
| 118,5014ms | 122,2432ms |     118,7843ms      |
| 118,1158ms | 120,9835ms |     119,0083ms      |
| 117,9952ms | 124,4349ms |      120,568ms      |
| 119,8493ms | 121,7712ms |     118,7769ms      |
| 118,6422ms | 117,9084ms |     118,0728ms      |
| 119,5745ms | 121,5589ms |     117,4068ms      |
| 119,1975ms | 122,9365ms |     117,5296ms      |
| 119,3941ms | 117,2969ms |     119,0755ms      |
| 119,169ms  | 118,6245ms |     118,7141ms      |
| 118,292ms  | 119,6189ms |      119,031ms      |
| 118,6837ms | 118,9646ms |     118,1212ms      |
| 119,9778ms | 119,0477ms |     117,5347ms      |
| 120,4208ms | 118,4733ms |     117,4734ms      |
| 118,3786ms | 118,7356ms |     118,5915ms      |
| 119,7454ms | 119,4169ms |     119,2887ms      |
| 118,2789ms | 119,0875ms |     118,0584ms      |
| 119,271ms  | 118,1464ms |     118,4983ms      |
| 119,335ms  | 119,5521ms |     118,0474ms      |
| 118,7005ms | 119,0694ms |     118,1803ms      |
| 118,3457ms | 118,0496ms |     118,1707ms      |
| 117,7015ms | 118,5116ms |     119,0759ms      |
| 118,7419ms | 119,3535ms |     119,1468ms      |
| 118,8379ms | 117,9011ms |     119,8446ms      |
| 118,0597ms | 118,3543ms |     122,2661ms      |
| 119,6309ms | 117,8641ms |      118,551ms      |
| 118,3332ms | 118,5553ms |     116,6742ms      |
| 117,8691ms | 118,4864ms |     122,1355ms      |
| 119,3503ms | 117,9589ms |     118,8551ms      |
| 118,004ms  | 117,8087ms |     120,3447ms      |
| 118,7784ms | 118,5959ms |     118,9898ms      |
| 117,6154ms | 117,5399ms |     118,5786ms      |
| 118,7265ms | 118,4837ms |     118,1598ms      |
| 118,9247ms | 119,4655ms |     118,1868ms      |
| 118,2822ms | 118,5293ms |     118,5529ms      |
| 117,6329ms | 118,3111ms |     120,2112ms      |
| 118,7938ms | 119,0674ms |     117,4496ms      |
| 118,7429ms | 120,2386ms |       117,8ms       |
| 118,6568ms | 119,8896ms |     116,7886ms      |
| 119,7951ms | 120,1887ms |     118,9057ms      |
| 118,8387ms | 118,8881ms |     118,8689ms      |
| 119,1598ms | 117,4524ms |     119,8262ms      |
| 119,1265ms | 119,0258ms |     119,8534ms      |
| 119,2243ms | 120,1392ms |     120,0119ms      |
| 118,2175ms | 118,7841ms |      119,753ms      |
| 118,4254ms | 118,5518ms |     119,7787ms      |
| 119,3031ms | 121,7947ms |     119,0739ms      |
| 118,5503ms | 120,1853ms |     120,0399ms      |
| 120,1332ms | 120,1572ms |     120,2151ms      |
| 119,9637ms | 119,2745ms |      119,409ms      |
| 118,6128ms |     -      |      120,398ms      |
|     -      |     -      |     119,9134ms      |
|     -      |     -      |     118,4986ms      |
|     -      |     -      |      117,78ms       |
|     -      |     -      |      116,873ms      |
|     -      |     -      |     118,3748ms      |
|     -      |     -      |     117,0593ms      |
|     -      |     -      |     117,9339ms      |
|     -      |     -      |     117,2271ms      |
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
| 117,2172ms | 134,1791ms |          -          |
| 117,3964ms | 132,3714ms |          -          |
| 122,6725ms | 129,6428ms |          -          |
| 120,6135ms | 163,3685ms |          -          |
| 121,0908ms | 129,163ms  |          -          |
|     -      | 117,091ms  |          -          |

### Distribution:

Callsite:
 117,5719ms - 117,7015ms: ████ (5)
 117,8691ms - 118,1158ms: ██████████ (12)
 118,2175ms - 118,4254ms: █████████ (11)
 118,4631ms - 118,7005ms: █████████ (11)
 118,7265ms - 118,9661ms: █████████ (11)
 119,0948ms - 119,2710ms: ██████████ (12)
 119,3031ms - 119,5858ms: ███████ (9)
 119,6309ms - 119,8493ms: █████ (6)
 119,9637ms - 120,1332ms: ██ (3)
 120,2239ms - 120,4548ms: ██ (3)

CallTarget:
 117,2969ms - 118,1660ms: ██████ (16)
 118,3071ms - 119,0875ms: ██████████ (24)
 119,2179ms - 120,1572ms: █████ (13)
 120,1853ms - 120,9835ms: ███ (9)
 121,1876ms - 122,0228ms: ██ (7)
 122,2227ms - 122,9365ms: █ (3)
 123,0860ms - 123,0860ms:  (1)
 124,0149ms - 124,4349ms:  (2)
 125,2240ms - 125,8132ms: █ (4)
 126,4343ms - 126,8590ms: █ (3)

CallTarget+Inlining:
 116,6634ms - 117,0593ms: ███ (6)
 117,2271ms - 117,7800ms: █████ (11)
 117,8000ms - 118,2422ms: ███████ (14)
 118,3748ms - 118,8689ms: ███████ (15)
 118,9057ms - 119,4104ms: ██████████ (19)
 119,5762ms - 120,0119ms: █████ (11)
 120,0399ms - 120,5680ms: █████ (10)
 120,8199ms - 120,9469ms: █ (3)
 -922337203685477,0000ms - -922337203685477,0000ms:  (0)
 122,1355ms - 122,2661ms: █ (2)

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


The json file 'X:\github\tonyredondo\timeitsharp\src\TimeItSharp\bin\Debug\net7.0\jsonexporter_876159174.json' was exported.
The Datadog exported ran successfully.
The Datadog profiler was successfully attached to the .NET processes.

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


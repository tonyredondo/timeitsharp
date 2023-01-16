# timeit
Command execution time meter allows to configure multiple scenarios to run benchmarks over CLI apps, output are available in markdown and as datadog traces.

### Usage
```bash
timeit [configuration file.json]
```

## Sample Configuration

```json
{
  "warmUpCount": 10,
  "count": 50,
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
  "processTimeout": 15,
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
/Users/tony.redondo/repos/github/tonyredondo/timeitsharp/src/TimeIt/bin/Debug/net7.0/TimeIt config-example.json
TimeIt by Tony Redondo

Warmup count: 10
Count: 80
Number of Scenarios: 3
Exporters: ConsoleExporter, JsonExporter

Scenario: Callsite
  Warming up ..........
    Duration: 1,3290525s
  Run ................................................................................
    Duration: 10,6179024s

Scenario: CallTarget
  Warming up ..........
    Duration: 1,2654448s
  Run ................................................................................
    Duration: 10,200122s

Scenario: CallTarget+Inlining
  Warming up ..........
    Duration: 1,2619734s
  Run ................................................................................
    Duration: 10,2538028s

### Results:
                                                 
|  Callsite  | CallTarget | CallTarget+Inlining |
| :--------: | :--------: | :-----------------: |
| 114,958ms  |  108,1ms   |      114,048ms      |
| 113,419ms  | 113,912ms  |      118,255ms      |
| 111,386ms  | 115,954ms  |      110,892ms      |
| 115,377ms  | 111,923ms  |      109,914ms      |
| 119,044ms  | 114,958ms  |      113,83ms       |
| 112,556ms  | 118,489ms  |      112,664ms      |
| 114,535ms  | 113,936ms  |      110,353ms      |
| 112,296ms  | 116,996ms  |      113,218ms      |
| 126,395ms  | 110,091ms  |      116,359ms      |
| 114,088ms  | 110,032ms  |      113,012ms      |
| 113,116ms  | 112,452ms  |       115,3ms       |
|  115,08ms  | 114,179ms  |      116,529ms      |
| 119,278ms  | 108,782ms  |      117,401ms      |
| 116,149ms  | 111,753ms  |      114,363ms      |
| 119,402ms  | 108,724ms  |      111,78ms       |
| 124,311ms  | 110,961ms  |      112,531ms      |
| 118,242ms  | 115,258ms  |      111,385ms      |
| 113,532ms  |  108,87ms  |      109,271ms      |
| 121,131ms  | 108,495ms  |      108,179ms      |
| 118,323ms  |  111,95ms  |      108,362ms      |
| 116,078ms  | 112,262ms  |      110,989ms      |
| 115,277ms  | 109,323ms  |      109,59ms       |
|  122,74ms  | 113,474ms  |      113,685ms      |
| 117,081ms  | 108,166ms  |      114,221ms      |
| 118,404ms  | 112,661ms  |      110,733ms      |
| 117,197ms  |  110,28ms  |      116,139ms      |
| 117,504ms  | 109,644ms  |      109,398ms      |
| 118,294ms  | 110,881ms  |      108,069ms      |
| 124,936ms  | 107,754ms  |      111,875ms      |
| 123,953ms  | 116,419ms  |      115,009ms      |
| 124,029ms  | 113,084ms  |      113,113ms      |
| 121,522ms  | 110,044ms  |      110,198ms      |
| 119,776ms  | 114,037ms  |      113,869ms      |
| 117,903ms  | 111,018ms  |      112,267ms      |
| 113,112ms  | 113,101ms  |      110,932ms      |
| 115,609ms  | 115,765ms  |      111,937ms      |
| 125,398ms  | 115,865ms  |      119,381ms      |
| 116,528ms  | 111,068ms  |      109,974ms      |
| 112,642ms  | 112,083ms  |      111,547ms      |
| 114,113ms  | 109,297ms  |      106,43ms       |
| 117,481ms  | 114,384ms  |      107,193ms      |
| 115,182ms  | 111,384ms  |      111,701ms      |
| 114,776ms  | 112,181ms  |      110,182ms      |
|  112,62ms  | 111,593ms  |      107,986ms      |
| 112,166ms  | 110,675ms  |      108,396ms      |
| 111,001ms  | 108,227ms  |      111,591ms      |
| 116,012ms  | 109,519ms  |      116,806ms      |
| 117,271ms  | 107,344ms  |      110,55ms       |
| 117,395ms  | 111,272ms  |      113,728ms      |
| 117,475ms  | 112,879ms  |      109,747ms      |
| 118,163ms  | 108,842ms  |      120,066ms      |
| 113,539ms  | 115,485ms  |      107,603ms      |
| 117,539ms  | 109,445ms  |       108,7ms       |
| 111,781ms  | 110,746ms  |      111,628ms      |
| 116,978ms  | 111,459ms  |      110,438ms      |
|  113,76ms  | 116,496ms  |      114,036ms      |
| 116,023ms  | 110,049ms  |      114,598ms      |
| 114,149ms  | 116,412ms  |      108,332ms      |
| 114,828ms  | 111,745ms  |      112,269ms      |
| 120,443ms  | 109,909ms  |      107,286ms      |
| 114,479ms  | 111,436ms  |      112,488ms      |
| 113,945ms  | 112,848ms  |      109,629ms      |
| 118,046ms  | 110,998ms  |      110,901ms      |
| 108,598ms  |  107,85ms  |      109,553ms      |
| 110,477ms  | 110,551ms  |      115,832ms      |
|  113,93ms  | 109,699ms  |      114,991ms      |
| 108,122ms  | 117,246ms  |      109,27ms       |
|  112,01ms  | 110,673ms  |      112,469ms      |
| 110,091ms  |  109,76ms  |      107,862ms      |
| 119,245ms  | 108,077ms  |      109,73ms       |
| 113,854ms  | 108,836ms  |      118,656ms      |
| 114,681ms  | 110,064ms  |      116,897ms      |
| 114,711ms  | 111,186ms  |      115,513ms      |
| 108,506ms  | 107,682ms  |      116,577ms      |
| 111,924ms  | 114,462ms  |      117,471ms      |
|  118,11ms  | 116,755ms  |      105,918ms      |
| 116,1055ms | 113,187ms  |      108,618ms      |
| 116,1055ms | 108,162ms  |     112,1066ms      |
| 116,1055ms | 111,6866ms |     112,1066ms      |
| 116,1055ms | 111,6866ms |     112,1066ms      |
                                                 
### Outliers:
                                                
| Callsite  | CallTarget | CallTarget+Inlining |
| :-------: | :--------: | :-----------------: |
| 129,807ms | 123,682ms  |      121,057ms      |
| 129,602ms |  126,17ms  |      120,365ms      |
| 127,994ms |     -      |      127,635ms      |
| 134,758ms |     -      |          -          |
                                                
### Summary:
                                                                                                                                  
| Name                                      | Mean       | StdDev   | StdErr   | P99        | P95        | P90        | Outliers |
| ----------------------------------------- | ---------- | -------- | -------- | ---------- | ---------- | ---------- | -------- |
| Callsite                                  | 116,1055ms | 3,8681ms | 0,4324ms | 126,2587ms | 124,2123ms | 121,3786ms | 4        |
| ├>process.start.time_ms                   | 23,20444   | 1,021072 | 0,114159 | 27,451093  | 24,64418   | 24,359393  |          |
| ├>runtime.dotnet.cpu.percent              | 0,0525     | 0,010966 | 0,001226 | 0,1        | 0,0825     | 0,05       |          |
| ├>runtime.dotnet.cpu.system               | 1,704012   | 0,089846 | 0,010045 | 1,939583   | 1,8794     | 1,831633   |          |
| ├>runtime.dotnet.cpu.user                 | 6,89675    | 0,276045 | 0,030863 | 7,506807   | 7,42765    | 7,3534     |          |
| ├>runtime.dotnet.mem.committed            | 0          | 0        | 0        | 0          | 0          | 0          |          |
| ├>runtime.dotnet.threads.contention_count | 0          | 0        | 0        | 0          | 0          | 0          |          |
| ├>runtime.dotnet.threads.contention_time  | 0          | 0        | 0        | 0          | 0          | 0          |          |
| ├>runtime.dotnet.threads.count            | 13,55      | 0,21932  | 0,024521 | 14,5       | 14,15      | 13,5       |          |
| └>runtime.dotnet.threads.workers_count    | 0          | 0        | 0        | 0          | 0          | 0          |          |
|                                           |            |          |          |            |            |            |          |
| CallTarget                                | 111,6866ms | 2,7056ms | 0,3025ms | 118,3191ms | 116,6643ms | 115,9213ms | 2        |
| ├>process.start.time_ms                   | 22,31021   | 0,885134 | 0,098961 | 28,368075  | 23,40332   | 22,814607  |          |
| ├>runtime.dotnet.cpu.percent              | 0,05       | 0        | 0        | 0,05       | 0,05       | 0,05       |          |
| ├>runtime.dotnet.cpu.system               | 1,6156     | 0,04551  | 0,005088 | 1,775907   | 1,70035    | 1,674267   |          |
| ├>runtime.dotnet.cpu.user                 | 6,565987   | 0,135456 | 0,015144 | 6,971843   | 6,84675    | 6,7787     |          |
| ├>runtime.dotnet.mem.committed            | 0          | 0        | 0        | 0          | 0          | 0          |          |
| ├>runtime.dotnet.threads.contention_count | 0          | 0        | 0        | 0          | 0          | 0          |          |
| ├>runtime.dotnet.threads.contention_time  | 0          | 0        | 0        | 0          | 0          | 0          |          |
| ├>runtime.dotnet.threads.count            | 13,6375    | 0,346547 | 0,038745 | 14,5       | 14,5       | 14,5       |          |
| └>runtime.dotnet.threads.workers_count    | 0          | 0        | 0        | 0          | 0          | 0          |          |
|                                           |            |          |          |            |            |            |          |
| CallTarget+Inlining                       | 112,1066ms | 3,2255ms | 0,3606ms | 119,9723ms | 117,9806ms | 116,722ms  | 3        |
| ├>process.start.time_ms                   | 22,449442  | 0,650316 | 0,072708 | 24,750904  | 23,52246   | 23,208593  |          |
| ├>runtime.dotnet.cpu.percent              | 0,05       | 0        | 0        | 0,05       | 0,05       | 0,05       |          |
| ├>runtime.dotnet.cpu.system               | 1,6254     | 0,056876 | 0,006359 | 1,80749    | 1,74265    | 1,696367   |          |
| ├>runtime.dotnet.cpu.user                 | 6,615888   | 0,206348 | 0,02307  | 7,1458     | 7,03495    | 6,954533   |          |
| ├>runtime.dotnet.mem.committed            | 0          | 0        | 0        | 0          | 0          | 0          |          |
| ├>runtime.dotnet.threads.contention_count | 0          | 0        | 0        | 0          | 0          | 0          |          |
| ├>runtime.dotnet.threads.contention_time  | 0          | 0        | 0        | 0          | 0          | 0          |          |
| ├>runtime.dotnet.threads.count            | 13,5875    | 0,284349 | 0,031791 | 14,5       | 14,5       | 13,5       |          |
| └>runtime.dotnet.threads.workers_count    | 0          | 0        | 0        | 0          | 0          | 0          |          |
|                                           |            |          |          |            |            |            |          |
                                                                                                                                  

The json file '/Users/tony.redondo/repos/github/tonyredondo/timeitsharp/src/TimeIt/bin/Debug/net7.0/jsonexporter_1342382910.json' was exported.

```

## Output is markdown compatible

Example:

### Results:

|  Callsite  | CallTarget | CallTarget+Inlining |
| :--------: | :--------: | :-----------------: |
| 114,958ms  |  108,1ms   |      114,048ms      |
| 113,419ms  | 113,912ms  |      118,255ms      |
| 111,386ms  | 115,954ms  |      110,892ms      |
| 115,377ms  | 111,923ms  |      109,914ms      |
| 119,044ms  | 114,958ms  |      113,83ms       |
| 112,556ms  | 118,489ms  |      112,664ms      |
...

### Outliers:

| Callsite  | CallTarget | CallTarget+Inlining |
| :-------: | :--------: | :-----------------: |
| 129,807ms | 123,682ms  |      121,057ms      |
| 129,602ms |  126,17ms  |      120,365ms      |
| 127,994ms |     -      |      127,635ms      |
| 134,758ms |     -      |          -          |

### Summary:

| Name                                      | Mean       | StdDev   | StdErr   | P99        | P95        | P90        | Outliers |
| ----------------------------------------- | ---------- | -------- | -------- | ---------- | ---------- | ---------- | -------- |
| Callsite                                  | 116,1055ms | 3,8681ms | 0,4324ms | 126,2587ms | 124,2123ms | 121,3786ms | 4        |
| ├>process.start.time_ms                   | 23,20444   | 1,021072 | 0,114159 | 27,451093  | 24,64418   | 24,359393  |          |
| ├>runtime.dotnet.cpu.percent              | 0,0525     | 0,010966 | 0,001226 | 0,1        | 0,0825     | 0,05       |          |
| ├>runtime.dotnet.cpu.system               | 1,704012   | 0,089846 | 0,010045 | 1,939583   | 1,8794     | 1,831633   |          |
| ├>runtime.dotnet.cpu.user                 | 6,89675    | 0,276045 | 0,030863 | 7,506807   | 7,42765    | 7,3534     |          |
| ├>runtime.dotnet.mem.committed            | 0          | 0        | 0        | 0          | 0          | 0          |          |
| ├>runtime.dotnet.threads.contention_count | 0          | 0        | 0        | 0          | 0          | 0          |          |
| ├>runtime.dotnet.threads.contention_time  | 0          | 0        | 0        | 0          | 0          | 0          |          |
| ├>runtime.dotnet.threads.count            | 13,55      | 0,21932  | 0,024521 | 14,5       | 14,15      | 13,5       |          |
| └>runtime.dotnet.threads.workers_count    | 0          | 0        | 0        | 0          | 0          | 0          |          |
|                                           |            |          |          |            |            |            |          |
| CallTarget                                | 111,6866ms | 2,7056ms | 0,3025ms | 118,3191ms | 116,6643ms | 115,9213ms | 2        |
| ├>process.start.time_ms                   | 22,31021   | 0,885134 | 0,098961 | 28,368075  | 23,40332   | 22,814607  |          |
| ├>runtime.dotnet.cpu.percent              | 0,05       | 0        | 0        | 0,05       | 0,05       | 0,05       |          |
| ├>runtime.dotnet.cpu.system               | 1,6156     | 0,04551  | 0,005088 | 1,775907   | 1,70035    | 1,674267   |          |
| ├>runtime.dotnet.cpu.user                 | 6,565987   | 0,135456 | 0,015144 | 6,971843   | 6,84675    | 6,7787     |          |
| ├>runtime.dotnet.mem.committed            | 0          | 0        | 0        | 0          | 0          | 0          |          |
| ├>runtime.dotnet.threads.contention_count | 0          | 0        | 0        | 0          | 0          | 0          |          |
| ├>runtime.dotnet.threads.contention_time  | 0          | 0        | 0        | 0          | 0          | 0          |          |
| ├>runtime.dotnet.threads.count            | 13,6375    | 0,346547 | 0,038745 | 14,5       | 14,5       | 14,5       |          |
| └>runtime.dotnet.threads.workers_count    | 0          | 0        | 0        | 0          | 0          | 0          |          |
|                                           |            |          |          |            |            |            |          |
| CallTarget+Inlining                       | 112,1066ms | 3,2255ms | 0,3606ms | 119,9723ms | 117,9806ms | 116,722ms  | 3        |
| ├>process.start.time_ms                   | 22,449442  | 0,650316 | 0,072708 | 24,750904  | 23,52246   | 23,208593  |          |
| ├>runtime.dotnet.cpu.percent              | 0,05       | 0        | 0        | 0,05       | 0,05       | 0,05       |          |
| ├>runtime.dotnet.cpu.system               | 1,6254     | 0,056876 | 0,006359 | 1,80749    | 1,74265    | 1,696367   |          |
| ├>runtime.dotnet.cpu.user                 | 6,615888   | 0,206348 | 0,02307  | 7,1458     | 7,03495    | 6,954533   |          |
| ├>runtime.dotnet.mem.committed            | 0          | 0        | 0        | 0          | 0          | 0          |          |
| ├>runtime.dotnet.threads.contention_count | 0          | 0        | 0        | 0          | 0          | 0          |          |
| ├>runtime.dotnet.threads.contention_time  | 0          | 0        | 0        | 0          | 0          | 0          |          |
| ├>runtime.dotnet.threads.count            | 13,5875    | 0,284349 | 0,031791 | 14,5       | 14,5       | 13,5       |          |
| └>runtime.dotnet.threads.workers_count    | 0          | 0        | 0        | 0          | 0          | 0          |          |
|                                           |            |          |          |            |            |            |          |
  
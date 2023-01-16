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
Count: 10
Number of Scenarios: 3
Exporters: ConsoleExporter, JsonExporter

Scenario: Callsite
  Warming up ..........
    Duration: 1,3538563s
  Run ..................................................
    Duration: 6,5443463s

Scenario: CallTarget
  Warming up ..........
    Duration: 1,3713682s
  Run ..................................................
    Duration: 6,6169059s

Scenario: CallTarget+Inlining
  Warming up ..........
    Duration: 1,3205544s
  Run ..................................................
    Duration: 6,3888984s

### Results:
                                                 
|  Callsite  | CallTarget | CallTarget+Inlining |
| :--------: | :--------: | :-----------------: |
| 109,334ms  |  115,6ms   |      121,619ms      |
| 109,737ms  | 122,537ms  |      120,768ms      |
|  111,71ms  | 122,254ms  |      112,194ms      |
| 112,685ms  |  119,83ms  |      113,11ms       |
| 110,852ms  | 119,016ms  |      113,263ms      |
| 108,575ms  | 114,041ms  |      116,125ms      |
| 109,793ms  | 121,068ms  |      117,323ms      |
| 111,941ms  |  116,12ms  |      122,281ms      |
| 109,261ms  | 116,185ms  |      117,457ms      |
| 109,579ms  | 118,404ms  |      116,245ms      |
| 108,864ms  | 118,534ms  |      118,856ms      |
| 110,643ms  | 117,967ms  |      112,563ms      |
| 110,702ms  | 122,789ms  |      119,844ms      |
| 118,292ms  | 120,328ms  |      115,943ms      |
| 120,461ms  | 119,685ms  |      119,703ms      |
| 113,675ms  | 124,181ms  |      113,333ms      |
| 115,551ms  | 125,233ms  |      113,177ms      |
| 115,489ms  | 119,958ms  |      109,846ms      |
| 114,901ms  | 127,513ms  |      114,445ms      |
| 115,601ms  | 121,601ms  |      114,337ms      |
| 115,262ms  | 121,416ms  |      115,231ms      |
| 115,526ms  | 121,744ms  |      114,866ms      |
| 120,075ms  |  114,25ms  |      119,769ms      |
| 120,693ms  | 120,043ms  |      120,465ms      |
| 121,227ms  | 118,185ms  |      123,716ms      |
|  112,53ms  | 117,928ms  |      115,699ms      |
| 114,026ms  | 119,366ms  |      115,906ms      |
| 116,888ms  | 117,186ms  |      113,216ms      |
| 118,915ms  | 121,757ms  |      109,157ms      |
| 117,816ms  | 113,227ms  |      121,888ms      |
| 118,696ms  | 120,657ms  |      122,184ms      |
| 115,323ms  | 115,121ms  |      116,798ms      |
|  119,7ms   | 117,609ms  |      116,39ms       |
| 117,526ms  | 114,412ms  |      109,859ms      |
| 115,925ms  | 117,259ms  |      110,167ms      |
| 119,581ms  | 120,812ms  |      116,255ms      |
| 118,314ms  | 115,755ms  |      116,995ms      |
| 114,137ms  | 118,485ms  |      110,39ms       |
| 113,176ms  | 115,072ms  |      120,838ms      |
| 118,309ms  | 120,798ms  |      110,542ms      |
| 114,998ms  | 117,595ms  |      111,404ms      |
| 120,186ms  | 120,636ms  |      110,691ms      |
| 121,146ms  | 120,158ms  |      109,556ms      |
| 122,491ms  | 117,122ms  |      109,151ms      |
|  133,15ms  | 116,653ms  |      110,774ms      |
| 133,618ms  | 120,259ms  |      108,807ms      |
| 129,431ms  | 127,583ms  |      111,232ms      |
| 134,756ms  | 119,2325ms |      112,167ms      |
| 134,328ms  | 119,2325ms |      109,252ms      |
| 117,0488ms | 119,2325ms |     115,0162ms      |
                                                 
### Outliers:
                                                
| Callsite  | CallTarget | CallTarget+Inlining |
| :-------: | :--------: | :-----------------: |
| 167,888ms | 137,783ms  |      125,812ms      |
|     -     | 107,544ms  |          -          |
|     -     | 132,417ms  |          -          |
                                                
### Summary:
                                                                                                                                 
| Name                                      | Mean       | StdDev   | StdErr   | P99       | P95        | P90        | Outliers |
| ----------------------------------------- | ---------- | -------- | -------- | --------- | ---------- | ---------- | -------- |
| Callsite                                  | 117,0488ms | 6,6027ms | 0,9337ms | 134,756ms | 133,7245ms | 126,8863ms | 1        |
| ├>runtime.dotnet.cpu.percent              | 0,127333   | 0,03791  | 0,005361 | 0,233333  | 0,205      | 0,166667   |          |
| ├>runtime.dotnet.cpu.system               | 2,401203   | 0,260201 | 0,036798 | 3,844667  | 2,787167   | 2,592778   |          |
| ├>runtime.dotnet.cpu.user                 | 12,02938   | 3,474718 | 0,491399 | 19,699333 | 18,763333  | 17,372333  |          |
| ├>runtime.dotnet.mem.committed            | 0          | 0        | 0        | 0         | 0          | 0          |          |
| ├>runtime.dotnet.threads.contention_count | 2,28       | 1,750102 | 0,247502 | 5         | 5          | 5          |          |
| ├>runtime.dotnet.threads.contention_time  | 1,14857    | 1,685792 | 0,238407 | 6,66257   | 5,324206   | 3,336175   |          |
| ├>runtime.dotnet.threads.count            | 15,845     | 0,525433 | 0,074307 | 17,333333 | 16,333333  | 16,333333  |          |
| └>runtime.dotnet.threads.workers_count    | 1,666667   | 0,322968 | 0,045675 | 2         | 2          | 2          |          |
|                                           |            |          |          |           |            |            |          |
| CallTarget                                | 119,2325ms | 3,1686ms | 0,4481ms | 127,583ms | 125,575ms  | 122,6966ms | 3        |
| ├>runtime.dotnet.cpu.percent              | 0,141333   | 0,041818 | 0,005914 | 0,233333  | 0,233333   | 0,2        |          |
| ├>runtime.dotnet.cpu.system               | 2,545667   | 0,413704 | 0,058507 | 4,184     | 4,057233   | 2,598089   |          |
| ├>runtime.dotnet.cpu.user                 | 13,140453  | 3,530084 | 0,499229 | 18,141333 | 17,8378    | 17,498778  |          |
| ├>runtime.dotnet.mem.committed            | 0          | 0        | 0        | 0         | 0          | 0          |          |
| ├>runtime.dotnet.threads.contention_count | 3,24       | 1,597958 | 0,225985 | 5         | 5          | 5          |          |
| ├>runtime.dotnet.threads.contention_time  | 0,807424   | 1,149245 | 0,162528 | 5,082097  | 3,534911   | 2,703825   |          |
| ├>runtime.dotnet.threads.count            | 16,093333  | 0,398637 | 0,056376 | 16,333333 | 16,333333  | 16,333333  |          |
| └>runtime.dotnet.threads.workers_count    | 1,846667   | 0,253859 | 0,035901 | 2         | 2          | 2          |          |
|                                           |            |          |          |           |            |            |          |
| CallTarget+Inlining                       | 115,0162ms | 4,2097ms | 0,5953ms | 123,716ms | 122,1985ms | 121,3326ms | 1        |
| ├>runtime.dotnet.cpu.percent              | 0,12       | 0,030117 | 0,004259 | 0,2       | 0,171667   | 0,166667   |          |
| ├>runtime.dotnet.cpu.system               | 2,38676    | 0,242779 | 0,034334 | 3,847333  | 2,638833   | 2,576689   |          |
| ├>runtime.dotnet.cpu.user                 | 11,25992   | 2,876793 | 0,40684  | 17,722667 | 17,121833  | 16,936533  |          |
| ├>runtime.dotnet.mem.committed            | 0          | 0        | 0        | 0         | 0          | 0          |          |
| ├>runtime.dotnet.threads.contention_count | 2,04       | 1,5513   | 0,219387 | 6         | 5          | 4          |          |
| ├>runtime.dotnet.threads.contention_time  | 0,821975   | 1,551486 | 0,219413 | 6,661681  | 4,86841    | 3,331557   |          |
| ├>runtime.dotnet.threads.count            | 15,766667  | 0,522726 | 0,073925 | 17,333333 | 16,333333  | 16,333333  |          |
| └>runtime.dotnet.threads.workers_count    | 1,586667   | 0,312694 | 0,044222 | 2         | 2          | 2          |          |
|                                           |            |          |          |           |            |            |          |
                                                                                                                                 

The json file '/Users/tony.redondo/repos/github/tonyredondo/timeitsharp/src/TimeIt/bin/Debug/net7.0/jsonexporter_1420335707.json' was exported.

```

## Output is markdown compatible

Example:

### Results:

| Callsite  | CallTarget | CallTarget+Inlining |
| :-------: | :--------: | :-----------------: |
| 72,8958ms | 70,1548ms  |      72,0776ms      |
| 72,1798ms | 70,7505ms  |      72,0274ms      |
| 71,4647ms | 70,1769ms  |      71,2185ms      |
| 73,184ms  | 70,9668ms  |      71,436ms       |
| 71,6795ms | 70,7545ms  |      70,5807ms      |
...

### Outliers:

| Callsite  | CallTarget | CallTarget+Inlining |
| :-------: | :--------: | :-----------------: |
| 73,9385ms | 73,1094ms  |      73,5732ms      |
| 70,154ms  | 73,1236ms  |       73,51ms       |
|     -     | 72,6894ms  |      74,2013ms      |

### Summary:

| Name                                      | Mean       | StdDev   | StdErr   | P99       | P95        | P90        | Outliers |
| ----------------------------------------- | ---------- | -------- | -------- | --------- | ---------- | ---------- | -------- |
| Callsite                                  | 117,0488ms | 6,6027ms | 0,9337ms | 134,756ms | 133,7245ms | 126,8863ms | 1        |
| ├>runtime.dotnet.cpu.percent              | 0,127333   | 0,03791  | 0,005361 | 0,233333  | 0,205      | 0,166667   |          |
| ├>runtime.dotnet.cpu.system               | 2,401203   | 0,260201 | 0,036798 | 3,844667  | 2,787167   | 2,592778   |          |
| ├>runtime.dotnet.cpu.user                 | 12,02938   | 3,474718 | 0,491399 | 19,699333 | 18,763333  | 17,372333  |          |
| ├>runtime.dotnet.mem.committed            | 0          | 0        | 0        | 0         | 0          | 0          |          |
| ├>runtime.dotnet.threads.contention_count | 2,28       | 1,750102 | 0,247502 | 5         | 5          | 5          |          |
| ├>runtime.dotnet.threads.contention_time  | 1,14857    | 1,685792 | 0,238407 | 6,66257   | 5,324206   | 3,336175   |          |
| ├>runtime.dotnet.threads.count            | 15,845     | 0,525433 | 0,074307 | 17,333333 | 16,333333  | 16,333333  |          |
| └>runtime.dotnet.threads.workers_count    | 1,666667   | 0,322968 | 0,045675 | 2         | 2          | 2          |          |
|                                           |            |          |          |           |            |            |          |
| CallTarget                                | 119,2325ms | 3,1686ms | 0,4481ms | 127,583ms | 125,575ms  | 122,6966ms | 3        |
| ├>runtime.dotnet.cpu.percent              | 0,141333   | 0,041818 | 0,005914 | 0,233333  | 0,233333   | 0,2        |          |
| ├>runtime.dotnet.cpu.system               | 2,545667   | 0,413704 | 0,058507 | 4,184     | 4,057233   | 2,598089   |          |
| ├>runtime.dotnet.cpu.user                 | 13,140453  | 3,530084 | 0,499229 | 18,141333 | 17,8378    | 17,498778  |          |
| ├>runtime.dotnet.mem.committed            | 0          | 0        | 0        | 0         | 0          | 0          |          |
| ├>runtime.dotnet.threads.contention_count | 3,24       | 1,597958 | 0,225985 | 5         | 5          | 5          |          |
| ├>runtime.dotnet.threads.contention_time  | 0,807424   | 1,149245 | 0,162528 | 5,082097  | 3,534911   | 2,703825   |          |
| ├>runtime.dotnet.threads.count            | 16,093333  | 0,398637 | 0,056376 | 16,333333 | 16,333333  | 16,333333  |          |
| └>runtime.dotnet.threads.workers_count    | 1,846667   | 0,253859 | 0,035901 | 2         | 2          | 2          |          |
|                                           |            |          |          |           |            |            |          |
| CallTarget+Inlining                       | 115,0162ms | 4,2097ms | 0,5953ms | 123,716ms | 122,1985ms | 121,3326ms | 1        |
| ├>runtime.dotnet.cpu.percent              | 0,12       | 0,030117 | 0,004259 | 0,2       | 0,171667   | 0,166667   |          |
| ├>runtime.dotnet.cpu.system               | 2,38676    | 0,242779 | 0,034334 | 3,847333  | 2,638833   | 2,576689   |          |
| ├>runtime.dotnet.cpu.user                 | 11,25992   | 2,876793 | 0,40684  | 17,722667 | 17,121833  | 16,936533  |          |
| ├>runtime.dotnet.mem.committed            | 0          | 0        | 0        | 0         | 0          | 0          |          |
| ├>runtime.dotnet.threads.contention_count | 2,04       | 1,5513   | 0,219387 | 6         | 5          | 4          |          |
| ├>runtime.dotnet.threads.contention_time  | 0,821975   | 1,551486 | 0,219413 | 6,661681  | 4,86841    | 3,331557   |          |
| ├>runtime.dotnet.threads.count            | 15,766667  | 0,522726 | 0,073925 | 17,333333 | 16,333333  | 16,333333  |          |
| └>runtime.dotnet.threads.workers_count    | 1,586667   | 0,312694 | 0,044222 | 2         | 2          | 2          |          |
|                                           |            |          |          |           |            |            |          |
    
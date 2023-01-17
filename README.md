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
Count: 100
Number of Scenarios: 3
Exporters: ConsoleExporter, JsonExporter

Scenario: Callsite
  Warming up ..........
    Duration: 1,3769666s
  Run ....................................................................................................
    Duration: 13,8324159s

Scenario: CallTarget
  Warming up ..........
    Duration: 1,4461922s
  Run ....................................................................................................
    Duration: 13,8850524s

Scenario: CallTarget+Inlining
  Warming up ..........
    Duration: 1,3392805s
  Run ....................................................................................................
    Duration: 14,0172962s

### Results:
                                                 
|  Callsite  | CallTarget | CallTarget+Inlining |
| :--------: | :--------: | :-----------------: |
| 117,402ms  | 129,217ms  |      112,923ms      |
| 116,783ms  | 120,397ms  |      118,085ms      |
| 114,381ms  | 122,812ms  |      113,417ms      |
| 111,995ms  | 127,772ms  |      116,596ms      |
| 112,034ms  | 121,548ms  |      117,935ms      |
| 117,023ms  | 123,578ms  |      112,357ms      |
| 113,507ms  | 120,506ms  |      113,844ms      |
| 116,778ms  | 124,506ms  |      115,682ms      |
| 118,166ms  | 119,169ms  |      114,962ms      |
| 120,125ms  | 115,772ms  |      120,813ms      |
|  114,05ms  | 119,663ms  |      121,845ms      |
| 115,949ms  | 123,445ms  |      117,335ms      |
| 114,676ms  | 125,986ms  |      114,764ms      |
| 113,076ms  | 121,608ms  |      118,136ms      |
| 113,498ms  | 114,916ms  |      125,311ms      |
|  112,82ms  | 117,556ms  |      115,332ms      |
| 110,136ms  | 119,083ms  |      117,998ms      |
| 112,826ms  | 121,169ms  |      118,829ms      |
| 112,473ms  |  121,36ms  |      114,624ms      |
| 109,883ms  | 118,434ms  |      119,409ms      |
| 118,031ms  | 110,516ms  |      126,145ms      |
| 120,072ms  | 115,525ms  |      130,214ms      |
| 116,822ms  |  129,03ms  |      117,918ms      |
|   118ms    | 116,681ms  |      129,266ms      |
| 120,348ms  | 118,244ms  |      118,584ms      |
| 121,172ms  | 119,557ms  |      126,83ms       |
| 127,907ms  | 115,678ms  |      118,261ms      |
| 116,872ms  | 117,248ms  |      122,427ms      |
|  116,07ms  | 116,465ms  |      118,239ms      |
| 117,507ms  | 126,932ms  |      116,657ms      |
| 119,078ms  |  117,75ms  |      115,225ms      |
| 119,041ms  | 130,203ms  |      128,748ms      |
| 119,397ms  |  125,3ms   |      116,863ms      |
| 121,665ms  | 118,988ms  |      123,292ms      |
| 120,889ms  | 118,454ms  |      127,655ms      |
| 121,444ms  | 116,416ms  |      115,113ms      |
| 118,174ms  | 113,689ms  |      114,821ms      |
| 117,284ms  | 116,567ms  |      117,621ms      |
| 113,911ms  | 110,542ms  |      126,107ms      |
| 121,759ms  | 113,342ms  |      129,557ms      |
| 118,322ms  | 115,657ms  |      123,421ms      |
| 113,405ms  |  118,82ms  |      123,174ms      |
| 117,695ms  | 119,714ms  |      126,998ms      |
| 115,544ms  | 120,165ms  |      122,138ms      |
| 116,043ms  | 112,247ms  |      116,163ms      |
| 115,733ms  | 113,424ms  |      127,458ms      |
| 116,979ms  | 119,907ms  |      112,098ms      |
| 124,026ms  | 110,424ms  |      126,967ms      |
| 115,578ms  |  113,08ms  |      124,855ms      |
| 116,603ms  | 116,023ms  |      115,288ms      |
| 120,425ms  | 114,096ms  |      121,19ms       |
| 116,731ms  | 110,515ms  |      121,75ms       |
| 120,883ms  | 118,349ms  |      118,908ms      |
| 117,416ms  | 110,934ms  |      122,811ms      |
| 116,931ms  | 114,978ms  |      118,182ms      |
|  119,82ms  | 118,353ms  |       115,8ms       |
| 128,838ms  | 115,834ms  |      126,077ms      |
| 117,245ms  | 115,427ms  |      118,842ms      |
| 120,016ms  | 120,005ms  |      116,342ms      |
| 119,601ms  | 122,259ms  |      115,362ms      |
| 115,399ms  | 119,131ms  |      118,014ms      |
| 118,499ms  |  120,44ms  |      115,549ms      |
| 123,003ms  | 121,441ms  |      115,597ms      |
| 118,445ms  | 117,237ms  |      121,944ms      |
| 116,635ms  | 122,108ms  |      115,711ms      |
| 113,074ms  | 124,445ms  |      117,687ms      |
| 125,964ms  | 120,588ms  |      121,071ms      |
| 116,743ms  | 121,212ms  |      121,401ms      |
| 117,215ms  | 121,565ms  |      115,333ms      |
| 118,269ms  | 117,721ms  |      120,137ms      |
| 116,897ms  |  122,06ms  |      120,287ms      |
| 127,659ms  | 118,194ms  |      118,919ms      |
| 125,184ms  | 115,962ms  |      118,042ms      |
| 119,049ms  | 129,054ms  |      123,209ms      |
|  115,97ms  | 114,224ms  |      119,288ms      |
| 114,356ms  | 117,939ms  |      118,686ms      |
| 116,667ms  | 123,091ms  |      121,59ms       |
| 122,072ms  | 122,116ms  |      122,676ms      |
|  121,11ms  | 122,161ms  |      121,776ms      |
| 116,239ms  | 121,756ms  |      127,874ms      |
| 117,393ms  | 118,181ms  |      116,679ms      |
| 114,764ms  | 113,899ms  |      112,352ms      |
| 123,812ms  |  114,34ms  |      116,803ms      |
| 116,353ms  | 117,268ms  |      116,46ms       |
| 127,384ms  | 126,584ms  |      123,774ms      |
| 120,862ms  | 127,328ms  |      122,043ms      |
| 124,118ms  | 116,196ms  |      116,623ms      |
| 114,658ms  | 114,677ms  |      121,42ms       |
| 117,909ms  | 128,839ms  |      127,595ms      |
| 118,908ms  | 126,636ms  |      117,853ms      |
| 123,605ms  | 113,896ms  |      130,302ms      |
| 125,727ms  | 112,387ms  |      130,022ms      |
| 120,407ms  | 113,046ms  |      125,154ms      |
| 111,799ms  | 119,286ms  |      121,505ms      |
| 122,426ms  | 116,178ms  |      124,378ms      |
| 118,0992ms |  117,41ms  |      120,325ms      |
| 118,0992ms | 119,0044ms |     120,1212ms      |
| 118,0992ms | 119,0044ms |     120,1212ms      |
| 118,0992ms | 119,0044ms |     120,1212ms      |
| 118,0992ms | 119,0044ms |     120,1212ms      |
                                                 
### Outliers:
                                                
| Callsite  | CallTarget | CallTarget+Inlining |
| :-------: | :--------: | :-----------------: |
| 130,158ms | 132,314ms  |      108,676ms      |
| 131,437ms | 135,558ms  |      136,595ms      |
| 131,914ms | 131,832ms  |      133,857ms      |
| 147,716ms | 131,009ms  |      136,144ms      |
| 130,597ms |     -      |          -          |
                                                
### Summary:
                                                                                                                                             
| Name                                      | Mean       | StdDev   | StdErr   | Min       | Max       | P95        | P90        | Outliers |
| ----------------------------------------- | ---------- | -------- | -------- | --------- | --------- | ---------- | ---------- | -------- |
| Callsite                                  | 118,0992ms | 3,8775ms | 0,3877ms | 109,883ms | 128,838ms | 125,881ms  | 123,7361ms | 5        |
| ├>process.internal_duration_ms            | 90,939645  | 3,141635 | 0,318985 | 83,968    | 98,816    | 96,9728    | 95,819093  | 3        |
| ├>process.time_to_end_ms                  | 4,37291    | 2,419346 | 0,241935 | 2,4554    | 12,2078   | 9,67116    | 8,666093   | N/A      |
| ├>process.time_to_start_ms                | 22,994825  | 0,767237 | 0,077901 | 21,5968   | 24,5786   | 24,3794    | 24,251747  | 3        |
| ├>runtime.dotnet.cpu.percent              | 0,05       | 0,002418 | 0,000247 | 0,033333  | 0,066667  | 0,05       | 0,05       | 4        |
| ├>runtime.dotnet.cpu.system               | 1,705417   | 0,075541 | 0,00771  | 1,558     | 1,866     | 1,8437     | 1,818      | 4        |
| ├>runtime.dotnet.cpu.user                 | 6,880906   | 0,215413 | 0,021986 | 6,495     | 7,502     | 7,2414     | 7,1924     | 4        |
| ├>runtime.dotnet.mem.committed            | 0          | 0        | 0        | 0         | 0         | 0          | 0          | 0        |
| ├>runtime.dotnet.threads.contention_count | 0          | 0        | 0        | 0         | 0         | 0          | 0          | 0        |
| ├>runtime.dotnet.threads.contention_time  | 0          | 0        | 0        | 0         | 0         | 0          | 0          | 0        |
| ├>runtime.dotnet.threads.count            | 13,626667  | 0,377629 | 0,037763 | 13,5      | 15,333333 | 14,5       | 14,5       | N/A      |
| └>runtime.dotnet.threads.workers_count    | 0          | 0        | 0        | 0         | 0         | 0          | 0          | 2        |
|                                           |            |          |          |           |           |            |            |          |
| CallTarget                                | 119,0044ms | 4,6165ms | 0,4616ms | 110,424ms | 130,203ms | 128,4655ms | 126,3647ms | 4        |
| ├>process.internal_duration_ms            | 90,778667  | 3,131357 | 0,319593 | 84,48     | 97,8944   | 96,4864    | 95,040853  | 4        |
| ├>process.time_to_end_ms                  | 4,91781    | 2,376252 | 0,241272 | 2,4422    | 9,9024    | 8,97912    | 8,309173   | 3        |
| ├>process.time_to_start_ms                | 23,150745  | 0,816668 | 0,082496 | 21,3798   | 25,3884   | 24,62045   | 24,334     | 2        |
| ├>runtime.dotnet.cpu.percent              | 0,05       | 0        | 0        | 0,05      | 0,05      | 0,05       | 0,05       | 4        |
| ├>runtime.dotnet.cpu.system               | 1,693292   | 0,062884 | 0,006418 | 1,544     | 1,856     | 1,8017     | 1,783233   | 4        |
| ├>runtime.dotnet.cpu.user                 | 6,893351   | 0,221653 | 0,022505 | 6,414     | 7,484     | 7,2932     | 7,220867   | 3        |
| ├>runtime.dotnet.mem.committed            | 0          | 0        | 0        | 0         | 0         | 0          | 0          | 0        |
| ├>runtime.dotnet.threads.contention_count | 0          | 0        | 0        | 0         | 0         | 0          | 0          | 0        |
| ├>runtime.dotnet.threads.contention_time  | 0          | 0        | 0        | 0         | 0         | 0          | 0          | 0        |
| ├>runtime.dotnet.threads.count            | 13,608333  | 0,336162 | 0,033616 | 13,5      | 15,333333 | 14,5       | 14,133333  | N/A      |
| └>runtime.dotnet.threads.workers_count    | 0          | 0        | 0        | 0         | 0         | 0          | 0          | 1        |
|                                           |            |          |          |           |           |            |            |          |
| CallTarget+Inlining                       | 120,1212ms | 4,5897ms | 0,4589ms | 112,098ms | 130,302ms | 129,0847ms | 127,2893ms | 4        |
| ├>process.internal_duration_ms            | 91,485238  | 3,633443 | 0,372783 | 84,5824   | 100,0448  | 98,048     | 96,064853  | 5        |
| ├>process.time_to_end_ms                  | 4,999066   | 2,677323 | 0,271841 | 2,3772    | 10,2382   | 9,8362     | 9,719853   | 3        |
| ├>process.time_to_start_ms                | 23,09732   | 0,730861 | 0,074208 | 21,46     | 24,7136   | 24,27948   | 23,998787  | 3        |
| ├>runtime.dotnet.cpu.percent              | 0,05       | 0        | 0        | 0,05      | 0,05      | 0,05       | 0,05       | 0        |
| ├>runtime.dotnet.cpu.system               | 1,70544    | 0,079398 | 0,00794  | 1,529     | 1,901     | 1,85195    | 1,806433   | N/A      |
| ├>runtime.dotnet.cpu.user                 | 6,975365   | 0,239489 | 0,024443 | 6,468     | 7,477     | 7,3845     | 7,285667   | 4        |
| ├>runtime.dotnet.mem.committed            | 0          | 0        | 0        | 0         | 0         | 0          | 0          | 0        |
| ├>runtime.dotnet.threads.contention_count | 0          | 0        | 0        | 0         | 0         | 0          | 0          | 0        |
| ├>runtime.dotnet.threads.contention_time  | 0          | 0        | 0        | 0         | 0         | 0          | 0          | 0        |
| ├>runtime.dotnet.threads.count            | 13,5       | 0        | 0        | 13,5      | 13,5      | 13,5       | 13,5       | 3        |
| └>runtime.dotnet.threads.workers_count    | 0          | 0        | 0        | 0         | 0         | 0          | 0          | 2        |
|                                           |            |          |          |           |           |            |            |          |
                                                                                                                                             

The json file '/Users/tony.redondo/repos/github/tonyredondo/timeitsharp/src/TimeIt/bin/Debug/net7.0/jsonexporter_1741499399.json' was exported.

```

## Output is markdown compatible

Example:

### Results:

|  Callsite  | CallTarget | CallTarget+Inlining |
| :--------: | :--------: | :-----------------: |
| 117,402ms  | 129,217ms  |      112,923ms      |
| 116,783ms  | 120,397ms  |      118,085ms      |
| 114,381ms  | 122,812ms  |      113,417ms      |
| 111,995ms  | 127,772ms  |      116,596ms      |
| 112,034ms  | 121,548ms  |      117,935ms      |
| 117,023ms  | 123,578ms  |      112,357ms      |
| 113,507ms  | 120,506ms  |      113,844ms      |
...

### Outliers:

| Callsite  | CallTarget | CallTarget+Inlining |
| :-------: | :--------: | :-----------------: |
| 130,158ms | 132,314ms  |      108,676ms      |
| 131,437ms | 135,558ms  |      136,595ms      |
| 131,914ms | 131,832ms  |      133,857ms      |
| 147,716ms | 131,009ms  |      136,144ms      |
| 130,597ms |     -      |          -          |

### Summary:

| Name                                      | Mean       | StdDev   | StdErr   | Min       | Max       | P95        | P90        | Outliers |
| ----------------------------------------- | ---------- | -------- | -------- | --------- | --------- | ---------- | ---------- | -------- |
| Callsite                                  | 118,0992ms | 3,8775ms | 0,3877ms | 109,883ms | 128,838ms | 125,881ms  | 123,7361ms | 5        |
| ├>process.internal_duration_ms            | 90,939645  | 3,141635 | 0,318985 | 83,968    | 98,816    | 96,9728    | 95,819093  | 3        |
| ├>process.time_to_end_ms                  | 4,37291    | 2,419346 | 0,241935 | 2,4554    | 12,2078   | 9,67116    | 8,666093   | N/A      |
| ├>process.time_to_start_ms                | 22,994825  | 0,767237 | 0,077901 | 21,5968   | 24,5786   | 24,3794    | 24,251747  | 3        |
| ├>runtime.dotnet.cpu.percent              | 0,05       | 0,002418 | 0,000247 | 0,033333  | 0,066667  | 0,05       | 0,05       | 4        |
| ├>runtime.dotnet.cpu.system               | 1,705417   | 0,075541 | 0,00771  | 1,558     | 1,866     | 1,8437     | 1,818      | 4        |
| ├>runtime.dotnet.cpu.user                 | 6,880906   | 0,215413 | 0,021986 | 6,495     | 7,502     | 7,2414     | 7,1924     | 4        |
| ├>runtime.dotnet.mem.committed            | 0          | 0        | 0        | 0         | 0         | 0          | 0          | 0        |
| ├>runtime.dotnet.threads.contention_count | 0          | 0        | 0        | 0         | 0         | 0          | 0          | 0        |
| ├>runtime.dotnet.threads.contention_time  | 0          | 0        | 0        | 0         | 0         | 0          | 0          | 0        |
| ├>runtime.dotnet.threads.count            | 13,626667  | 0,377629 | 0,037763 | 13,5      | 15,333333 | 14,5       | 14,5       | N/A      |
| └>runtime.dotnet.threads.workers_count    | 0          | 0        | 0        | 0         | 0         | 0          | 0          | 2        |
|                                           |            |          |          |           |           |            |            |          |
| CallTarget                                | 119,0044ms | 4,6165ms | 0,4616ms | 110,424ms | 130,203ms | 128,4655ms | 126,3647ms | 4        |
| ├>process.internal_duration_ms            | 90,778667  | 3,131357 | 0,319593 | 84,48     | 97,8944   | 96,4864    | 95,040853  | 4        |
| ├>process.time_to_end_ms                  | 4,91781    | 2,376252 | 0,241272 | 2,4422    | 9,9024    | 8,97912    | 8,309173   | 3        |
| ├>process.time_to_start_ms                | 23,150745  | 0,816668 | 0,082496 | 21,3798   | 25,3884   | 24,62045   | 24,334     | 2        |
| ├>runtime.dotnet.cpu.percent              | 0,05       | 0        | 0        | 0,05      | 0,05      | 0,05       | 0,05       | 4        |
| ├>runtime.dotnet.cpu.system               | 1,693292   | 0,062884 | 0,006418 | 1,544     | 1,856     | 1,8017     | 1,783233   | 4        |
| ├>runtime.dotnet.cpu.user                 | 6,893351   | 0,221653 | 0,022505 | 6,414     | 7,484     | 7,2932     | 7,220867   | 3        |
| ├>runtime.dotnet.mem.committed            | 0          | 0        | 0        | 0         | 0         | 0          | 0          | 0        |
| ├>runtime.dotnet.threads.contention_count | 0          | 0        | 0        | 0         | 0         | 0          | 0          | 0        |
| ├>runtime.dotnet.threads.contention_time  | 0          | 0        | 0        | 0         | 0         | 0          | 0          | 0        |
| ├>runtime.dotnet.threads.count            | 13,608333  | 0,336162 | 0,033616 | 13,5      | 15,333333 | 14,5       | 14,133333  | N/A      |
| └>runtime.dotnet.threads.workers_count    | 0          | 0        | 0        | 0         | 0         | 0          | 0          | 1        |
|                                           |            |          |          |           |           |            |            |          |
| CallTarget+Inlining                       | 120,1212ms | 4,5897ms | 0,4589ms | 112,098ms | 130,302ms | 129,0847ms | 127,2893ms | 4        |
| ├>process.internal_duration_ms            | 91,485238  | 3,633443 | 0,372783 | 84,5824   | 100,0448  | 98,048     | 96,064853  | 5        |
| ├>process.time_to_end_ms                  | 4,999066   | 2,677323 | 0,271841 | 2,3772    | 10,2382   | 9,8362     | 9,719853   | 3        |
| ├>process.time_to_start_ms                | 23,09732   | 0,730861 | 0,074208 | 21,46     | 24,7136   | 24,27948   | 23,998787  | 3        |
| ├>runtime.dotnet.cpu.percent              | 0,05       | 0        | 0        | 0,05      | 0,05      | 0,05       | 0,05       | 0        |
| ├>runtime.dotnet.cpu.system               | 1,70544    | 0,079398 | 0,00794  | 1,529     | 1,901     | 1,85195    | 1,806433   | N/A      |
| ├>runtime.dotnet.cpu.user                 | 6,975365   | 0,239489 | 0,024443 | 6,468     | 7,477     | 7,3845     | 7,285667   | 4        |
| ├>runtime.dotnet.mem.committed            | 0          | 0        | 0        | 0         | 0         | 0          | 0          | 0        |
| ├>runtime.dotnet.threads.contention_count | 0          | 0        | 0        | 0         | 0         | 0          | 0          | 0        |
| ├>runtime.dotnet.threads.contention_time  | 0          | 0        | 0        | 0         | 0         | 0          | 0          | 0        |
| ├>runtime.dotnet.threads.count            | 13,5       | 0        | 0        | 13,5      | 13,5      | 13,5       | 13,5       | 3        |
| └>runtime.dotnet.threads.workers_count    | 0          | 0        | 0        | 0         | 0         | 0          | 0          | 2        |
|                                           |            |          |          |           |           |            |            |          |

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
    Duration: 0,8253198s
  Run ..................................................
    Duration: 3,769452s

Scenario: CallTarget
  Warming up ..........
    Duration: 0,7322082s
  Run ..................................................
    Duration: 3,6961071s

Scenario: CallTarget+Inlining
  Warming up ..........
    Duration: 0,7407789s
  Run ..................................................
    Duration: 3,8387445s

### Results:

| Callsite  | CallTarget | CallTarget+Inlining |
| :-------: | :--------: | :-----------------: |
| 72,8958ms | 70,1548ms  |      72,0776ms      |
| 72,1798ms | 70,7505ms  |      72,0274ms      |
| 71,4647ms | 70,1769ms  |      71,2185ms      |
| 73,184ms  | 70,9668ms  |      71,436ms       |
| 71,6795ms | 70,7545ms  |      70,5807ms      |
| 72,2581ms | 70,4225ms  |      71,4997ms      |
| 70,7451ms | 70,5339ms  |      71,5247ms      |
| 71,9065ms | 70,0012ms  |      70,606ms       |
| 72,2037ms | 69,7115ms  |      71,6554ms      |
| 72,8192ms | 70,5311ms  |      70,6804ms      |
| 72,1157ms | 69,9058ms  |      72,2887ms      |
| 72,659ms  | 71,5519ms  |      71,1286ms      |
| 71,8171ms | 70,8756ms  |      72,1491ms      |
| 72,1525ms | 70,2691ms  |      71,1567ms      |
| 71,6673ms | 71,2785ms  |      71,361ms       |
| 71,4522ms | 71,0414ms  |      71,1042ms      |
| 72,529ms  | 70,2079ms  |      70,7031ms      |
| 72,7155ms | 70,4511ms  |      71,3071ms      |
| 72,4723ms | 70,2148ms  |      71,1441ms      |
| 73,1249ms | 71,1588ms  |      72,892ms       |
| 71,1539ms | 69,7538ms  |      71,4271ms      |
| 72,6126ms | 70,8314ms  |      72,1325ms      |
| 72,6462ms |  70,028ms  |      73,0164ms      |
| 72,1947ms | 70,0561ms  |      72,1875ms      |
| 72,6114ms | 70,8802ms  |      72,5655ms      |
| 73,0477ms | 70,9312ms  |      72,6384ms      |
| 73,0255ms | 71,4823ms  |      71,6569ms      |
| 71,7495ms | 71,1338ms  |      71,4689ms      |
| 72,4466ms | 70,0194ms  |      72,2128ms      |
| 71,452ms  | 70,8558ms  |      71,3914ms      |
| 71,3713ms | 72,3678ms  |      71,0126ms      |
| 71,8018ms | 70,4588ms  |      72,466ms       |
| 71,9191ms | 70,2248ms  |      71,9964ms      |
| 72,5108ms | 70,3535ms  |      71,4539ms      |
| 72,8973ms |  70,855ms  |      71,5138ms      |
| 71,5239ms | 71,1469ms  |      71,4627ms      |
| 71,9716ms | 71,1432ms  |      72,178ms       |
| 71,2035ms | 71,0698ms  |      72,3061ms      |
| 70,4752ms |  70,827ms  |      72,309ms       |
| 71,5433ms |  71,298ms  |      72,2268ms      |
| 71,0973ms | 70,9388ms  |      72,3229ms      |
| 70,727ms  |  70,889ms  |      71,993ms       |
| 71,9629ms |  70,679ms  |      72,5455ms      |
| 71,7214ms |  72,654ms  |      72,0289ms      |
| 70,715ms  | 72,5252ms  |      72,933ms       |
| 71,9731ms |  72,514ms  |      71,7021ms      |
| 71,2332ms | 71,7566ms  |      72,1989ms      |
| 70,9282ms | 70,8219ms  |      71,7848ms      |
| 71,9699ms | 70,8219ms  |      71,7848ms      |
| 71,9699ms | 70,8219ms  |      71,7848ms      |

### Outliers:

| Callsite  | CallTarget | CallTarget+Inlining |
| :-------: | :--------: | :-----------------: |
| 73,9385ms | 73,1094ms  |      73,5732ms      |
| 70,154ms  | 73,1236ms  |       73,51ms       |
|     -     | 72,6894ms  |      74,2013ms      |

### Summary:

| Name                | Mean      | StdDev   | StdErr   | P99       | P95       | P90       | Outliers |
| ------------------- | --------- | -------- | -------- | --------- | --------- | --------- | -------- |
| Callsite            | 71,9699ms | 0,6936ms | 0,0981ms | 73,184ms  | 73,0592ms | 72,8967ms | 2        |
| CallTarget          | 70,8219ms | 0,6886ms | 0,0973ms | 72,654ms  | 72,5156ms | 71,6815ms | 3        |
| CallTarget+Inlining | 71,7848ms | 0,609ms  | 0,0861ms | 73,0164ms | 72,8981ms | 72,5581ms | 3        |


The json file 'C:\github\tonyredondo\timeitsharp\src\TimeIt\jsonexporter_196480544.json' was exported.

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

| Name                | Mean      | StdDev   | StdErr   | P99       | P95       | P90       | Outliers |
| ------------------- | --------- | -------- | -------- | --------- | --------- | --------- | -------- |
| Callsite            | 71,9699ms | 0,6936ms | 0,0981ms | 73,184ms  | 73,0592ms | 72,8967ms | 2        |
| CallTarget          | 70,8219ms | 0,6886ms | 0,0973ms | 72,654ms  | 72,5156ms | 71,6815ms | 3        |
| CallTarget+Inlining | 71,7848ms | 0,609ms  | 0,0861ms | 73,0164ms | 72,8981ms | 72,5581ms | 3        |

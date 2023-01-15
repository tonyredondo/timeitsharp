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
Exporters: JsonExporter

Scenario: Callsite
  Warming up ..........
    Duration: 0,8364209s
  Run ..................................................
    Duration: 4,0827127s

Scenario: CallTarget
  Warming up ..........
    Duration: 0,8106721s
  Run ..................................................
    Duration: 4,0260594s

Scenario: CallTarget+Inlining
  Warming up ..........
    Duration: 0,8014591s
  Run ..................................................
    Duration: 4,1355645s

                    Results                     
╭───────────┬────────────┬─────────────────────╮
│ Callsite  │ CallTarget │ CallTarget+Inlining │
├───────────┼────────────┼─────────────────────┤
│ 72,997ms  │  70,015ms  │      67,032ms       │
│ 75,451ms  │  68,04ms   │      65,729ms       │
│ 67,905ms  │  67,23ms   │      73,757ms       │
│ 68,758ms  │  70,379ms  │      68,345ms       │
│  65,83ms  │  70,019ms  │       65,49ms       │
│  70,56ms  │  69,88ms   │      75,498ms       │
│ 70,942ms  │  65,656ms  │      66,224ms       │
│ 70,638ms  │  64,962ms  │       67,03ms       │
│  67,04ms  │  67,073ms  │      67,989ms       │
│ 74,269ms  │  69,905ms  │      71,442ms       │
│ 70,406ms  │  64,915ms  │      71,178ms       │
│  71,94ms  │  65,076ms  │      71,064ms       │
│ 70,199ms  │  69,128ms  │      71,766ms       │
│ 70,925ms  │  69,625ms  │      73,931ms       │
│  73,47ms  │  64,035ms  │       67,24ms       │
│ 66,942ms  │  64,156ms  │      71,683ms       │
│ 71,689ms  │  70,847ms  │      71,523ms       │
│ 69,643ms  │  64,72ms   │      71,097ms       │
│ 66,095ms  │  69,679ms  │      75,182ms       │
│  69,34ms  │  66,069ms  │      65,945ms       │
│ 71,763ms  │  68,322ms  │      68,868ms       │
│ 65,356ms  │  68,045ms  │      67,053ms       │
│ 66,451ms  │  65,049ms  │      70,367ms       │
│ 65,098ms  │  69,063ms  │      75,769ms       │
│ 65,523ms  │  71,063ms  │      70,198ms       │
│ 64,591ms  │  67,412ms  │       67,67ms       │
│  65,1ms   │  69,612ms  │      71,163ms       │
│ 68,915ms  │  68,801ms  │      69,435ms       │
│ 68,213ms  │  69,868ms  │      65,204ms       │
│ 65,103ms  │  65,816ms  │      69,961ms       │
│ 64,693ms  │  68,292ms  │      75,396ms       │
│ 68,957ms  │  71,126ms  │      73,021ms       │
│ 67,551ms  │  65,258ms  │      72,355ms       │
│ 67,646ms  │  68,974ms  │      70,896ms       │
│ 69,976ms  │  70,642ms  │      72,896ms       │
│ 70,945ms  │  69,095ms  │      65,714ms       │
│  64,82ms  │  68,187ms  │      65,719ms       │
│ 69,531ms  │  68,966ms  │      69,742ms       │
│ 67,709ms  │  67,481ms  │      70,424ms       │
│ 66,502ms  │  66,907ms  │      71,313ms       │
│ 70,163ms  │  64,958ms  │      70,371ms       │
│ 65,162ms  │  65,587ms  │       69,23ms       │
│ 69,901ms  │  64,349ms  │       71,03ms       │
│ 64,521ms  │  65,99ms   │      69,591ms       │
│ 66,933ms  │  65,634ms  │      66,514ms       │
│ 69,457ms  │  69,846ms  │      72,287ms       │
│ 68,559ms  │  70,883ms  │      71,082ms       │
│ 68,5995ms │  64,375ms  │      69,455ms       │
│ 68,5995ms │ 67,7293ms  │       75,99ms       │
│ 68,5995ms │ 67,7293ms  │      70,1603ms      │
╰───────────┴────────────┴─────────────────────╯
                   Outliers                    
╭──────────┬────────────┬─────────────────────╮
│ Callsite │ CallTarget │ CallTarget+Inlining │
├──────────┼────────────┼─────────────────────┤
│ 76,616ms │  75,837ms  │      77,005ms       │
│ 81,115ms │  76,105ms  │          -          │
│ 77,378ms │     -      │          -          │
╰──────────┴────────────┴─────────────────────╯
                                                Summary                                                
╭─────────────────────┬───────────┬──────────┬──────────┬──────────┬───────────┬───────────┬──────────╮
│ Name                │ Mean      │ StdDev   │ StdErr   │ P99      │ P95       │ P90       │ Outliers │
├─────────────────────┼───────────┼──────────┼──────────┼──────────┼───────────┼───────────┼──────────┤
│ Callsite            │ 68,5995ms │ 2,7019ms │ 0,3821ms │ 75,451ms │ 73,5898ms │ 71,8751ms │ 3        │
│ CallTarget          │ 67,7293ms │ 2,1877ms │ 0,3093ms │ 71,126ms │ 70,91ms   │ 70,5455ms │ 2        │
│ CallTarget+Inlining │ 70,1603ms │ 2,9368ms │ 0,4153ms │ 75,99ms  │ 75,5386ms │ 74,7233ms │ 1        │
╰─────────────────────┴───────────┴──────────┴──────────┴──────────┴───────────┴───────────┴──────────╯

The json file '/Users/tony.redondo/repos/github/tonyredondo/timeitsharp/src/TimeIt/bin/Debug/net7.0/jsonexporter_1913165705.json' was exported.

```

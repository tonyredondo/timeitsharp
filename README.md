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
TimeItSharp v0.1.16
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
      "name": "DatadogProfilerService"
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

TimeItSharp v0.1.16
Warmup count: 10
Count: 100
Number of Scenarios: 3
Exporters: ConsoleExporter, JsonExporter, Datadog
Assertors: DefaultAssertor
Services: NoopService, DatadogProfilerService

Scenario: Callsite
  Warming up ..........
    Duration: 18,022s
  Run ....................................................................................................
    Duration: 183,268s

Scenario: CallTarget
  Warming up ..........
    Duration: 17,901s
  Run ....................................................................................................
    Duration: 177,054s

Scenario: CallTarget+Inlining
  Warming up ..........
    Duration: 17,138s
  Run ....................................................................................................
    Duration: 176,731s

### Results:

|  Callsite   | CallTarget  | CallTarget+Inlining |
| :---------: | :---------: | :-----------------: |
| 1712,0355ms | 1809,2249ms |     1712,7451ms     |
| 1711,8519ms | 1812,707ms  |     1681,5321ms     |
| 1759,6334ms | 1761,4379ms |     1709,6565ms     |
| 1743,6047ms | 1782,0147ms |     1718,8594ms     |
| 1805,166ms  | 1759,5192ms |     1693,5514ms     |
| 1728,1286ms | 1721,4352ms |     1741,2108ms     |
| 1726,9731ms | 1809,1678ms |     1729,0586ms     |
| 1727,3671ms | 1762,9909ms |     1683,2086ms     |
| 1760,2616ms | 1769,7007ms |     1692,7196ms     |
| 1740,318ms  | 1728,7443ms |     1683,2427ms     |
| 1694,9397ms | 1730,3045ms |     1711,8535ms     |
| 1772,488ms  | 1791,301ms  |     1753,7779ms     |
| 1758,5648ms | 1759,556ms  |     1744,7893ms     |
| 1770,2129ms | 1819,0962ms |     1681,1516ms     |
| 1762,3458ms | 1746,6244ms |     1741,5067ms     |
| 1759,0538ms | 1765,0275ms |     1740,873ms      |
| 1726,4549ms | 1775,252ms  |     1696,2346ms     |
| 1759,2269ms | 1779,7219ms |     1744,4713ms     |
| 1731,2681ms | 1767,6617ms |     1727,8033ms     |
| 1743,478ms  | 1746,817ms  |     1712,9343ms     |
| 1819,3852ms | 1745,6788ms |     1725,1472ms     |
| 1730,5452ms | 1789,5299ms |     1709,2331ms     |
| 1744,3131ms | 1793,4586ms |     1713,4544ms     |
| 1698,4716ms | 1835,735ms  |     1726,0341ms     |
| 1763,3534ms | 1731,1909ms |     1695,6123ms     |
| 1710,515ms  | 1761,3074ms |     1698,4475ms     |
| 1763,1017ms | 1742,8739ms |     1710,1266ms     |
| 1747,0451ms | 1713,5304ms |     1695,1605ms     |
| 1742,3292ms | 1789,3941ms |     1701,6009ms     |
| 1716,5927ms | 1806,0775ms |     1697,5976ms     |
| 1730,5595ms | 1789,4396ms |     1698,3711ms     |
| 1714,7109ms | 1760,5576ms |     1729,1777ms     |
| 1779,277ms  | 1807,4681ms |     1772,8821ms     |
| 1759,782ms  | 1837,7094ms |     1728,9686ms     |
| 1775,1079ms | 1820,2698ms |     1699,3048ms     |
| 1728,2771ms | 1837,3627ms |     1697,9556ms     |
| 1743,438ms  | 1807,4316ms |     1686,4281ms     |
| 1776,5764ms | 1738,1716ms |     1730,7365ms     |
| 1743,2013ms | 1742,9412ms |     1715,8751ms     |
| 1729,5267ms | 1759,1538ms |     1712,5777ms     |
| 1710,895ms  | 1760,6198ms |     1698,9213ms     |
| 1801,8411ms | 1746,8487ms |     1695,4041ms     |
| 1788,4059ms | 1786,8286ms |     1723,8195ms     |
| 1743,0373ms | 1742,7919ms |     1683,4364ms     |
| 1761,3715ms | 1790,2992ms |     1741,9327ms     |
| 1760,4818ms | 1805,1251ms |     1727,9758ms     |
| 1773,8946ms | 1785,9882ms |     1742,9782ms     |
| 1740,9452ms | 1791,4015ms |     1725,8319ms     |
| 1730,496ms  | 1823,6339ms |     1726,7436ms     |
| 1788,5657ms | 1823,3078ms |     1713,1223ms     |
| 1727,6348ms | 1823,1085ms |     1771,4378ms     |
| 1742,0519ms | 1726,4092ms |     1741,223ms      |
| 1696,4017ms | 1806,6418ms |     1728,1647ms     |
| 1741,3756ms | 1683,0613ms |     1694,7828ms     |
| 1791,0202ms | 1808,123ms  |     1714,4326ms     |
| 1756,6777ms | 1822,8259ms |     1722,8026ms     |
| 1744,121ms  | 1759,102ms  |     1725,437ms      |
| 1712,5388ms | 1716,223ms  |     1757,7084ms     |
| 1727,5275ms | 1788,5648ms |     1682,3973ms     |
| 1792,3348ms | 1822,1408ms |     1729,5675ms     |
| 1724,6534ms | 1742,6151ms |     1725,1679ms     |
| 1976,2218ms | 1705,8983ms |     1756,8827ms     |
| 1730,4318ms | 1745,7135ms |     1690,746ms      |
| 1788,4166ms | 1679,7297ms |     1696,6972ms     |
| 1791,1102ms | 1711,8348ms |     1691,8506ms     |
| 1806,7579ms | 1730,2446ms |     1713,3716ms     |
| 1793,2581ms | 1695,1808ms |     1736,2358ms     |
| 1741,9318ms | 1678,5405ms |     1805,5481ms     |
| 1869,6069ms | 1773,6039ms |     1712,5382ms     |
| 1786,0646ms | 1710,1417ms |     1684,3112ms     |
| 1805,049ms  | 1711,3176ms |     1696,072ms      |
| 1771,7984ms | 1710,3687ms |     1740,4835ms     |
| 1808,9874ms | 1725,5298ms |     1757,8404ms     |
| 1740,8034ms | 1741,7316ms |     1713,5916ms     |
| 1839,7998ms | 1683,1864ms |     1681,7954ms     |
| 1797,0736ms | 1771,8577ms |     1728,8432ms     |
| 1772,4506ms | 1699,0737ms |     1743,4279ms     |
| 1835,1057ms | 1714,379ms  |     1682,5108ms     |
| 1854,3158ms | 1741,7556ms |     1711,4469ms     |
| 1819,1745ms | 1707,9758ms |     1756,2691ms     |
| 1820,4258ms | 1742,0838ms |     1709,8878ms     |
| 1790,7461ms | 1743,5837ms |     1773,5385ms     |
| 1805,9105ms | 1709,2508ms |     1710,7374ms     |
| 1710,1299ms | 1772,7934ms |     1697,0076ms     |
| 1821,0742ms | 1694,6805ms |          -          |
| 1806,668ms  | 1743,0322ms |          -          |
| 1775,3134ms | 1802,9804ms |          -          |
| 1714,7574ms | 1711,4609ms |          -          |
| 1850,6723ms | 1712,8047ms |          -          |
| 1835,1855ms | 1669,5232ms |          -          |
| 1853,4894ms | 1692,034ms  |          -          |
| 1792,8148ms | 1711,2491ms |          -          |
| 1758,8601ms | 1746,5024ms |          -          |
| 1824,0592ms | 1697,8429ms |          -          |
|      -      | 1727,1796ms |          -          |
|      -      | 1700,2008ms |          -          |
|      -      | 1712,8316ms |          -          |
|      -      |      -      |          -          |
|      -      |      -      |          -          |
|      -      |      -      |          -          |

### Outliers:

|  Callsite   | CallTarget  | CallTarget+Inlining |
| :---------: | :---------: | :-----------------: |
| 2786,0422ms | 1885,1344ms |     2709,2544ms     |
| 2712,1441ms | 1664,9316ms |     1667,0647ms     |
| 2736,9514ms | 1848,4335ms |     1650,6314ms     |
| 2738,2216ms |      -      |     1680,3987ms     |
| 2874,8266ms |      -      |     1651,3404ms     |
| 1978,7659ms |      -      |     1677,8969ms     |
|      -      |      -      |     1930,4749ms     |
|      -      |      -      |     2008,7525ms     |
|      -      |      -      |     1992,1655ms     |
|      -      |      -      |     1666,6164ms     |
|      -      |      -      |     1829,3078ms     |
|      -      |      -      |     1677,0121ms     |
|      -      |      -      |     1679,929ms      |
|      -      |      -      |     2770,4633ms     |
|      -      |      -      |     2680,0046ms     |
|      -      |      -      |     1669,4135ms     |

### Distribution:

Callsite:
 1694,9397ms - 1716,5927ms: ████ (12)
 1724,6534ms - 1747,0451ms: ██████████ (29)
 1756,6777ms - 1779,2770ms: ███████ (22)
 1786,0646ms - 1806,7579ms: █████ (17)
 1808,9874ms - 1835,1855ms: ██ (8)
 1839,7998ms - 1854,3158ms: █ (4)
 1869,6069ms - 1869,6069ms:  (1)
 -922337203685477,0000ms - -922337203685477,0000ms:  (0)
 -922337203685477,0000ms - -922337203685477,0000ms:  (0)
 1976,2218ms - 1976,2218ms:  (1)

CallTarget:
Scenario 'CallTarget' is Bimodal. Peak count: 3
 1669,5232ms - 1683,1864ms: ███ (5)
 1692,0340ms - 1700,2008ms: ███ (6)
 1705,8983ms - 1716,2230ms: ████████ (14)
 1721,4352ms - 1731,1909ms: █████ (8)
 1738,1716ms - 1746,8487ms: ██████████ (16)
 1759,1020ms - 1769,7007ms: ███████ (12)
 1771,8577ms - 1786,8286ms: █████ (8)
 1788,5648ms - 1802,9804ms: █████ (9)
 1805,1251ms - 1820,2698ms: ██████ (11)
 1822,1408ms - 1837,7094ms: █████ (8)

CallTarget+Inlining:
 1681,1516ms - 1693,5514ms: ███████ (14)
 1694,7828ms - 1701,6009ms: ████████ (15)
 1709,2331ms - 1715,8751ms: █████████ (17)
 1718,8594ms - 1730,7365ms: ██████████ (18)
 1736,2358ms - 1742,9782ms: ████ (8)
 1743,4279ms - 1753,7779ms: ██ (4)
 1756,2691ms - 1757,8404ms: ██ (4)
 1771,4378ms - 1773,5385ms: █ (3)
 -922337203685477,0000ms - -922337203685477,0000ms:  (0)
 1805,5481ms - 1805,5481ms:  (1)

### Summary:

| Name                                        | Status | Mean           | StdDev       | StdErr      | Min         | Median      | Max         | P95         | P90         | Outliers |
| ------------------------------------------- | ------ | -------------- | ------------ | ----------- | ----------- | ----------- | ----------- | ----------- | ----------- | -------- |
| Callsite                                    | Passed | 1766,5342ms    | 45,743ms     | 4,718ms     | 1694,9397ms | 1759,7077ms | 1976,2218ms | 1850,1286ms | 1821,7707ms | 6 {0,7}  |
|   ├>process.corrected_duration_ms           |        | 1730,316935    | 30,30465     | 3,431326    | 1682,735    | 1730,14375  | 1790,0988   | 1779,55635  | 1776,316283 | 12 {1,4} |
|   ├>process.internal_duration_ms            |        | 56,096237      | 1,409638     | 0,156626    | 53,76       | 55,808      | 59,1872     | 58,96192    | 58,272427   | 14 {0,8} |
|   ├>process.startuphook_overhead_ms         |        | 29,172462      | 0,809182     | 0,096032    | 27,9552     | 29,184      | 30,72       | 30,6176     | 30,4128     | 13 {1,4} |
|   ├>process.time_to_end_main_ms             |        | 1591,641781    | 27,194802    | 3,250401    | 1552,1721   | 1592,0604   | 1642,2909   | 1639,81562  | 1632,472447 | 13 {1,4} |
|   ├>process.time_to_end_ms                  |        | 1591,555473    | 27,202259    | 3,251292    | 1552,0697   | 1592,0092   | 1642,2909   | 1639,71322  | 1632,370047 | 13 {1,4} |
|   ├>process.time_to_main_ms                 |        | 111,093936     | 2,128916     | 0,260088    | 106,8526    | 111,256     | 115,3568    | 114,8944    | 114,142133  | 14 {1,4} |
|   ├>process.time_to_start_ms                |        | 81,656108      | 1,51857      | 0,186923    | 78,5269     | 81,5398     | 84,8243     | 84,48183    | 83,98788    | 15 {1,3} |
|   ├>runtime.dotnet.cpu.percent              |        | 0,738487       | 0,187774     | 0,021539    | 0,375       | 0,75        | 1           | 1           | 1           | 11 {1,4} |
|   ├>runtime.dotnet.cpu.system               |        | 390,268265     | 3,047947     | 0,356735    | 364,583333  | 390,625     | 390,625     | 390,625     | 390,625     | 15 {0,5} |
|   ├>runtime.dotnet.cpu.user                 |        | 155,924479     | 60,222969    | 6,733133    | 78,125      | 156,25      | 234,375     | 234,375     | 234,375     | 1 {1,3}  |
|   ├>runtime.dotnet.exceptions.count         |        | 1              | 0            | 0           | 1           | 1           | 1           | 1           | 1           | 0        |
|   ├>runtime.dotnet.mem.committed            |        | 9018957,575758 | 30887,615577 | 3802,002654 | 8970240     | 9017344     | 9084928     | 9076736     | 9072640     | 16 {1,3} |
|   ├>runtime.dotnet.threads.contention_count |        | 0              | 0            | 0           | 0           | 0           | 0           | 0           | 0           | 0        |
|   ├>runtime.dotnet.threads.contention_time  |        | 0              | 0            | 0           | 0           | 0           | 0           | 0           | 0           | 0        |
|   ├>runtime.dotnet.threads.count            |        | 17             | 0            | 0           | 17          | 17          | 17          | 17          | 17          | 0        |
|   ├>runtime.dotnet.threads.workers_count    |        | 0              | 0            | 0           | 0           | 0           | 0           | 0           | 0           | 0        |
|   ├>runtime.process.private_bytes           |        | 9018957,575758 | 30887,615577 | 3802,002654 | 8970240     | 9017344     | 9084928     | 9076736     | 9072640     | 16 {1,3} |
|   └>runtime.process.processor_time          |        | 230,777138     | 58,679478    | 6,730998    | 117,1875    | 234,375     | 312,5       | 312,5       | 312,5       | 11 {1,4} |
| CallTarget                                  | Passed | 1755,5808ms    | 43,2235ms    | 4,3886ms    | 1669,5232ms | 1746,8487ms | 1837,7094ms | 1823,2679ms | 1818,6702ms | 3 {2}    |
|   ├>process.corrected_duration_ms           |        | 1725,483945    | 33,09116     | 3,771088    | 1672,2429   | 1719,898    | 1782,2158   | 1780,99038  | 1776,376973 | 8 {1,5}  |
|   ├>process.internal_duration_ms            |        | 50,344119      | 0,714736     | 0,087319    | 49,2544     | 50,176      | 51,712      | 51,6096     | 51,500373   | 16 {1,2} |
|   ├>process.startuphook_overhead_ms         |        | 26,645333      | 0,637739     | 0,075158    | 25,6        | 26,7264     | 27,7504     | 27,648      | 27,5456     | 14 {1,4} |
|   ├>process.time_to_end_main_ms             |        | 1605,304849    | 31,551214    | 3,69279     | 1555,1572   | 1600,4117   | 1661,8323   | 1659,5816   | 1647,0121   | 10 {1,5} |
|   ├>process.time_to_end_ms                  |        | 1605,22349     | 31,545042    | 3,692068    | 1555,0709   | 1600,4117   | 1661,6275   | 1659,4792   | 1646,9097   | 10 {1,5} |
|   ├>process.time_to_main_ms                 |        | 93,355852      | 1,419978     | 0,170945    | 91,0894     | 93,1112     | 96,1111     | 95,93446    | 95,6993     | 16 {1,3} |
|   ├>process.time_to_start_ms                |        | 66,891103      | 1,007772     | 0,124048    | 65,405      | 66,85325    | 68,8535     | 68,80274    | 68,439097   | 15 {1,3} |
|   ├>runtime.dotnet.cpu.percent              |        | 0,569277       | 0,204049     | 0,022397    | 0,25        | 0,75        | 0,75        | 0,75        | 0,75        | 11 {1,6} |
|   ├>runtime.dotnet.cpu.system               |        | 342,548077     | 38,218718    | 4,00641     | 312,5       | 312,5       | 390,625     | 390,625     | 390,625     | 0        |
|   ├>runtime.dotnet.cpu.user                 |        | 150,799419     | 57,21032     | 6,169147    | 78,125      | 156,25      | 234,375     | 234,375     | 234,375     | 0        |
|   ├>runtime.dotnet.exceptions.count         |        | 1              | 0            | 0           | 1           | 1           | 1           | 1           | 1           | 0        |
|   ├>runtime.dotnet.mem.committed            |        | 9105882,898551 | 31871,297712 | 3836,853364 | 9056256     | 9101312     | 9170944     | 9156198,4   | 9150464     | 14 {1,3} |
|   ├>runtime.dotnet.threads.contention_count |        | 0              | 0            | 0           | 0           | 0           | 0           | 0           | 0           | 0        |
|   ├>runtime.dotnet.threads.contention_time  |        | 0              | 0            | 0           | 0           | 0           | 0           | 0           | 0           | 0        |
|   ├>runtime.dotnet.threads.count            |        | 17             | 0            | 0           | 17          | 17          | 17          | 17          | 17          | 0        |
|   ├>runtime.dotnet.threads.workers_count    |        | 0              | 0            | 0           | 0           | 0           | 0           | 0           | 0           | 0        |
|   ├>runtime.process.private_bytes           |        | 9105882,898551 | 31871,297712 | 3836,853364 | 9056256     | 9101312     | 9170944     | 9156198,4   | 9150464     | 14 {1,3} |
|   └>runtime.process.processor_time          |        | 177,899096     | 63,76536     | 6,999158    | 78,125      | 234,375     | 234,375     | 234,375     | 234,375     | 11 {1,6} |
| CallTarget+Inlining                         | Passed | 1717,9856ms    | 25,4678ms    | 2,7787ms    | 1681,1516ms | 1713,413ms  | 1805,5481ms | 1763,9592ms | 1754,3591ms | 16 {0,4} |
|   ├>process.corrected_duration_ms           |        | 1685,750551    | 19,598238    | 2,219063    | 1654,9372   | 1686,1593   | 1719,0761   | 1717,055425 | 1715,472517 | 11 {1,4} |
|   ├>process.internal_duration_ms            |        | 49,355316      | 0,652259     | 0,078523    | 48,3328     | 49,2544     | 50,5856     | 50,4832     | 50,2784     | 17 {1,3} |
|   ├>process.startuphook_overhead_ms         |        | 26,658966      | 0,826628     | 0,091286    | 25,1904     | 26,6752     | 28,3648     | 28,06784    | 27,8528     | 16 {1,1} |
|   ├>process.time_to_end_main_ms             |        | 1573,896153    | 16,835826    | 2,041644    | 1542,6652   | 1573,2282   | 1601,7912   | 1600,49405  | 1597,9986   | 15 {1,4} |
|   ├>process.time_to_end_ms                  |        | 1573,8314      | 16,836567    | 2,041734    | 1542,5628   | 1573,2282   | 1601,6888   | 1600,46845  | 1597,913267 | 15 {1,4} |
|   ├>process.time_to_main_ms                 |        | 93,124909      | 2,006212     | 0,225716    | 89,9792     | 93,1061     | 98,1067     | 97,18971    | 96,065427   | 18 {1,1} |
|   ├>process.time_to_start_ms                |        | 66,629472      | 1,422528     | 0,159043    | 64,3902     | 66,5111     | 69,9548     | 69,452125   | 68,605687   | 19 {1}   |
|   ├>runtime.dotnet.cpu.percent              |        | 0,746951       | 0,177838     | 0,019639    | 0,5         | 0,75        | 1           | 1           | 1           | 1 {1,5}  |
|   ├>runtime.dotnet.cpu.system               |        | 342,221467     | 38,13703     | 3,97606     | 312,5       | 312,5       | 390,625     | 390,625     | 390,625     | 0        |
|   ├>runtime.dotnet.cpu.user                 |        | 114,272388     | 39,247569    | 4,794854    | 78,125      | 78,125      | 156,25      | 156,25      | 156,25      | 16 {1}   |
|   ├>runtime.dotnet.exceptions.count         |        | 1              | 0            | 0           | 1           | 1           | 1           | 1           | 1           | 0        |
|   ├>runtime.dotnet.mem.committed            |        | 9184699,223881 | 27213,741843 | 3324,68772  | 9146368     | 9179136     | 9240576     | 9236480     | 9228288     | 14 {1,2} |
|   ├>runtime.dotnet.threads.contention_count |        | 0              | 0            | 0           | 0           | 0           | 0           | 0           | 0           | 0        |
|   ├>runtime.dotnet.threads.contention_time  |        | 0              | 0            | 0           | 0           | 0           | 0           | 0           | 0           | 0        |
|   ├>runtime.dotnet.threads.count            |        | 17             | 0            | 0           | 17          | 17          | 17          | 17          | 17          | 0        |
|   ├>runtime.dotnet.threads.workers_count    |        | 0              | 0            | 0           | 0           | 0           | 0           | 0           | 0           | 0        |
|   ├>runtime.process.private_bytes           |        | 9184699,223881 | 27213,741843 | 3324,68772  | 9146368     | 9179136     | 9240576     | 9236480     | 9228288     | 14 {1,2} |
|   └>runtime.process.processor_time          |        | 233,422256     | 55,574409    | 6,137167    | 156,25      | 234,375     | 312,5       | 312,5       | 312,5       | 1 {1,5}  |


### Overheads:

|                     | Callsite | CallTarget | CallTarget+Inlining |
| ------------------- | -------- | ---------- | ------------------- |
| Callsite            | --       | -0.6%      | -2.7%               |
| CallTarget          | 0.6%     | --         | -2.1%               |
| CallTarget+Inlining | 2.8%     | 2.2%       | --                  |


The json file 'X:\github\tonyredondo\timeitsharp\src\TimeItSharp\jsonexporter_2919115.json' was exported.
The Datadog exported ran successfully.
The Datadog profiler was successfully attached to the .NET processes.

```

## Output is markdown compatible

Example:

### Results:

|  Callsite   | CallTarget  | CallTarget+Inlining |
| :---------: | :---------: | :-----------------: |
| 1712,0355ms | 1809,2249ms |     1712,7451ms     |
| 1711,8519ms | 1812,707ms  |     1681,5321ms     |
| 1759,6334ms | 1761,4379ms |     1709,6565ms     |
| 1743,6047ms | 1782,0147ms |     1718,8594ms     |
| 1805,166ms  | 1759,5192ms |     1693,5514ms     |
| 1728,1286ms | 1721,4352ms |     1741,2108ms     |
| 1726,9731ms | 1809,1678ms |     1729,0586ms     |
| 1727,3671ms | 1762,9909ms |     1683,2086ms     |
...

### Outliers:

|  Callsite   | CallTarget  | CallTarget+Inlining |
| :---------: | :---------: | :-----------------: |
| 2786,0422ms | 1885,1344ms |     2709,2544ms     |
| 2712,1441ms | 1664,9316ms |     1667,0647ms     |
| 2736,9514ms | 1848,4335ms |     1650,6314ms     |
| 2738,2216ms |      -      |     1680,3987ms     |
| 2874,8266ms |      -      |     1651,3404ms     |
| 1978,7659ms |      -      |     1677,8969ms     |
|      -      |      -      |     1930,4749ms     |
|      -      |      -      |     2008,7525ms     |
|      -      |      -      |     1992,1655ms     |

### Summary:

| Name                                        | Status | Mean           | StdDev       | StdErr      | Min         | Median      | Max         | P95         | P90         | Outliers |
| ------------------------------------------- | ------ | -------------- | ------------ | ----------- | ----------- | ----------- | ----------- | ----------- | ----------- | -------- |
| Callsite                                    | Passed | 1766,5342ms    | 45,743ms     | 4,718ms     | 1694,9397ms | 1759,7077ms | 1976,2218ms | 1850,1286ms | 1821,7707ms | 6 {0,7}  |
|   ├>process.corrected_duration_ms           |        | 1730,316935    | 30,30465     | 3,431326    | 1682,735    | 1730,14375  | 1790,0988   | 1779,55635  | 1776,316283 | 12 {1,4} |
|   ├>process.internal_duration_ms            |        | 56,096237      | 1,409638     | 0,156626    | 53,76       | 55,808      | 59,1872     | 58,96192    | 58,272427   | 14 {0,8} |
|   ├>process.startuphook_overhead_ms         |        | 29,172462      | 0,809182     | 0,096032    | 27,9552     | 29,184      | 30,72       | 30,6176     | 30,4128     | 13 {1,4} |
|   ├>process.time_to_end_main_ms             |        | 1591,641781    | 27,194802    | 3,250401    | 1552,1721   | 1592,0604   | 1642,2909   | 1639,81562  | 1632,472447 | 13 {1,4} |
|   ├>process.time_to_end_ms                  |        | 1591,555473    | 27,202259    | 3,251292    | 1552,0697   | 1592,0092   | 1642,2909   | 1639,71322  | 1632,370047 | 13 {1,4} |
|   ├>process.time_to_main_ms                 |        | 111,093936     | 2,128916     | 0,260088    | 106,8526    | 111,256     | 115,3568    | 114,8944    | 114,142133  | 14 {1,4} |
|   ├>process.time_to_start_ms                |        | 81,656108      | 1,51857      | 0,186923    | 78,5269     | 81,5398     | 84,8243     | 84,48183    | 83,98788    | 15 {1,3} |
|   ├>runtime.dotnet.cpu.percent              |        | 0,738487       | 0,187774     | 0,021539    | 0,375       | 0,75        | 1           | 1           | 1           | 11 {1,4} |
|   ├>runtime.dotnet.cpu.system               |        | 390,268265     | 3,047947     | 0,356735    | 364,583333  | 390,625     | 390,625     | 390,625     | 390,625     | 15 {0,5} |
|   ├>runtime.dotnet.cpu.user                 |        | 155,924479     | 60,222969    | 6,733133    | 78,125      | 156,25      | 234,375     | 234,375     | 234,375     | 1 {1,3}  |
|   ├>runtime.dotnet.exceptions.count         |        | 1              | 0            | 0           | 1           | 1           | 1           | 1           | 1           | 0        |
|   ├>runtime.dotnet.mem.committed            |        | 9018957,575758 | 30887,615577 | 3802,002654 | 8970240     | 9017344     | 9084928     | 9076736     | 9072640     | 16 {1,3} |
|   ├>runtime.dotnet.threads.contention_count |        | 0              | 0            | 0           | 0           | 0           | 0           | 0           | 0           | 0        |
|   ├>runtime.dotnet.threads.contention_time  |        | 0              | 0            | 0           | 0           | 0           | 0           | 0           | 0           | 0        |
|   ├>runtime.dotnet.threads.count            |        | 17             | 0            | 0           | 17          | 17          | 17          | 17          | 17          | 0        |
|   ├>runtime.dotnet.threads.workers_count    |        | 0              | 0            | 0           | 0           | 0           | 0           | 0           | 0           | 0        |
|   ├>runtime.process.private_bytes           |        | 9018957,575758 | 30887,615577 | 3802,002654 | 8970240     | 9017344     | 9084928     | 9076736     | 9072640     | 16 {1,3} |
|   └>runtime.process.processor_time          |        | 230,777138     | 58,679478    | 6,730998    | 117,1875    | 234,375     | 312,5       | 312,5       | 312,5       | 11 {1,4} |
| CallTarget                                  | Passed | 1755,5808ms    | 43,2235ms    | 4,3886ms    | 1669,5232ms | 1746,8487ms | 1837,7094ms | 1823,2679ms | 1818,6702ms | 3 {2}    |
|   ├>process.corrected_duration_ms           |        | 1725,483945    | 33,09116     | 3,771088    | 1672,2429   | 1719,898    | 1782,2158   | 1780,99038  | 1776,376973 | 8 {1,5}  |
|   ├>process.internal_duration_ms            |        | 50,344119      | 0,714736     | 0,087319    | 49,2544     | 50,176      | 51,712      | 51,6096     | 51,500373   | 16 {1,2} |
|   ├>process.startuphook_overhead_ms         |        | 26,645333      | 0,637739     | 0,075158    | 25,6        | 26,7264     | 27,7504     | 27,648      | 27,5456     | 14 {1,4} |
|   ├>process.time_to_end_main_ms             |        | 1605,304849    | 31,551214    | 3,69279     | 1555,1572   | 1600,4117   | 1661,8323   | 1659,5816   | 1647,0121   | 10 {1,5} |
|   ├>process.time_to_end_ms                  |        | 1605,22349     | 31,545042    | 3,692068    | 1555,0709   | 1600,4117   | 1661,6275   | 1659,4792   | 1646,9097   | 10 {1,5} |
|   ├>process.time_to_main_ms                 |        | 93,355852      | 1,419978     | 0,170945    | 91,0894     | 93,1112     | 96,1111     | 95,93446    | 95,6993     | 16 {1,3} |
|   ├>process.time_to_start_ms                |        | 66,891103      | 1,007772     | 0,124048    | 65,405      | 66,85325    | 68,8535     | 68,80274    | 68,439097   | 15 {1,3} |
|   ├>runtime.dotnet.cpu.percent              |        | 0,569277       | 0,204049     | 0,022397    | 0,25        | 0,75        | 0,75        | 0,75        | 0,75        | 11 {1,6} |
|   ├>runtime.dotnet.cpu.system               |        | 342,548077     | 38,218718    | 4,00641     | 312,5       | 312,5       | 390,625     | 390,625     | 390,625     | 0        |
|   ├>runtime.dotnet.cpu.user                 |        | 150,799419     | 57,21032     | 6,169147    | 78,125      | 156,25      | 234,375     | 234,375     | 234,375     | 0        |
|   ├>runtime.dotnet.exceptions.count         |        | 1              | 0            | 0           | 1           | 1           | 1           | 1           | 1           | 0        |
|   ├>runtime.dotnet.mem.committed            |        | 9105882,898551 | 31871,297712 | 3836,853364 | 9056256     | 9101312     | 9170944     | 9156198,4   | 9150464     | 14 {1,3} |
|   ├>runtime.dotnet.threads.contention_count |        | 0              | 0            | 0           | 0           | 0           | 0           | 0           | 0           | 0        |
|   ├>runtime.dotnet.threads.contention_time  |        | 0              | 0            | 0           | 0           | 0           | 0           | 0           | 0           | 0        |
|   ├>runtime.dotnet.threads.count            |        | 17             | 0            | 0           | 17          | 17          | 17          | 17          | 17          | 0        |
|   ├>runtime.dotnet.threads.workers_count    |        | 0              | 0            | 0           | 0           | 0           | 0           | 0           | 0           | 0        |
|   ├>runtime.process.private_bytes           |        | 9105882,898551 | 31871,297712 | 3836,853364 | 9056256     | 9101312     | 9170944     | 9156198,4   | 9150464     | 14 {1,3} |
|   └>runtime.process.processor_time          |        | 177,899096     | 63,76536     | 6,999158    | 78,125      | 234,375     | 234,375     | 234,375     | 234,375     | 11 {1,6} |
| CallTarget+Inlining                         | Passed | 1717,9856ms    | 25,4678ms    | 2,7787ms    | 1681,1516ms | 1713,413ms  | 1805,5481ms | 1763,9592ms | 1754,3591ms | 16 {0,4} |
|   ├>process.corrected_duration_ms           |        | 1685,750551    | 19,598238    | 2,219063    | 1654,9372   | 1686,1593   | 1719,0761   | 1717,055425 | 1715,472517 | 11 {1,4} |
|   ├>process.internal_duration_ms            |        | 49,355316      | 0,652259     | 0,078523    | 48,3328     | 49,2544     | 50,5856     | 50,4832     | 50,2784     | 17 {1,3} |
|   ├>process.startuphook_overhead_ms         |        | 26,658966      | 0,826628     | 0,091286    | 25,1904     | 26,6752     | 28,3648     | 28,06784    | 27,8528     | 16 {1,1} |
|   ├>process.time_to_end_main_ms             |        | 1573,896153    | 16,835826    | 2,041644    | 1542,6652   | 1573,2282   | 1601,7912   | 1600,49405  | 1597,9986   | 15 {1,4} |
|   ├>process.time_to_end_ms                  |        | 1573,8314      | 16,836567    | 2,041734    | 1542,5628   | 1573,2282   | 1601,6888   | 1600,46845  | 1597,913267 | 15 {1,4} |
|   ├>process.time_to_main_ms                 |        | 93,124909      | 2,006212     | 0,225716    | 89,9792     | 93,1061     | 98,1067     | 97,18971    | 96,065427   | 18 {1,1} |
|   ├>process.time_to_start_ms                |        | 66,629472      | 1,422528     | 0,159043    | 64,3902     | 66,5111     | 69,9548     | 69,452125   | 68,605687   | 19 {1}   |
|   ├>runtime.dotnet.cpu.percent              |        | 0,746951       | 0,177838     | 0,019639    | 0,5         | 0,75        | 1           | 1           | 1           | 1 {1,5}  |
|   ├>runtime.dotnet.cpu.system               |        | 342,221467     | 38,13703     | 3,97606     | 312,5       | 312,5       | 390,625     | 390,625     | 390,625     | 0        |
|   ├>runtime.dotnet.cpu.user                 |        | 114,272388     | 39,247569    | 4,794854    | 78,125      | 78,125      | 156,25      | 156,25      | 156,25      | 16 {1}   |
|   ├>runtime.dotnet.exceptions.count         |        | 1              | 0            | 0           | 1           | 1           | 1           | 1           | 1           | 0        |
|   ├>runtime.dotnet.mem.committed            |        | 9184699,223881 | 27213,741843 | 3324,68772  | 9146368     | 9179136     | 9240576     | 9236480     | 9228288     | 14 {1,2} |
|   ├>runtime.dotnet.threads.contention_count |        | 0              | 0            | 0           | 0           | 0           | 0           | 0           | 0           | 0        |
|   ├>runtime.dotnet.threads.contention_time  |        | 0              | 0            | 0           | 0           | 0           | 0           | 0           | 0           | 0        |
|   ├>runtime.dotnet.threads.count            |        | 17             | 0            | 0           | 17          | 17          | 17          | 17          | 17          | 0        |
|   ├>runtime.dotnet.threads.workers_count    |        | 0              | 0            | 0           | 0           | 0           | 0           | 0           | 0           | 0        |
|   ├>runtime.process.private_bytes           |        | 9184699,223881 | 27213,741843 | 3324,68772  | 9146368     | 9179136     | 9240576     | 9236480     | 9228288     | 14 {1,2} |
|   └>runtime.process.processor_time          |        | 233,422256     | 55,574409    | 6,137167    | 156,25      | 234,375     | 312,5       | 312,5       | 312,5       | 1 {1,5}  |

### Overheads:

|                     | Callsite | CallTarget | CallTarget+Inlining |
| ------------------- | -------- | ---------- | ------------------- |
| Callsite            | --       | -0.6%      | -2.7%               |
| CallTarget          | 0.6%     | --         | -2.1%               |
| CallTarget+Inlining | 2.8%     | 2.2%       | --                  |

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


using DatadogTestLogger.Vendors.Datadog.Trace.Ci;
using TimeIt.Common.Configuration;
using TimeIt.Common.Exporter;
using TimeIt.Common.Results;

namespace TimeIt.DatadogExporter;

public class TimeItDatadogExporter : IExporter
{
    /// <inheritdoc />
    public string Name => "Datadog";

    /// <inheritdoc />
    public bool Enabled { get; private set; }

    /// <inheritdoc />
    public void SetConfiguration(Config configuration)
    {
        Enabled = true;
    }

    /// <inheritdoc />
    public void Export(IEnumerable<ScenarioResult> results)
    {
    }
}
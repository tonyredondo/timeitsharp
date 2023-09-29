using TimeItSharp.Common.Configuration;
using TimeItSharp.Common.Results;

namespace TimeItSharp.Common.Exporters;

public interface IExporter : INamedExtension
{
    bool Enabled { get; }

    void SetConfiguration(Config configuration);

    void Export(IEnumerable<ScenarioResult> results);
}
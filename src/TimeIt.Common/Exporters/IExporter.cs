using TimeIt.Common.Configuration;
using TimeIt.Common.Results;

namespace TimeIt.Common.Exporters;

public interface IExporter
{
    string Name { get; }

    bool Enabled { get; }

    void SetConfiguration(Config configuration);

    void Export(IEnumerable<ScenarioResult> results);
}
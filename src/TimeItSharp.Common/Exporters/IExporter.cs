using TimeItSharp.Common.Results;

namespace TimeItSharp.Common.Exporters;

public interface IExporter : INamedExtension
{
    bool Enabled { get; }

    void Initialize(InitOptions options);

    void Export(IEnumerable<ScenarioResult> results);
}
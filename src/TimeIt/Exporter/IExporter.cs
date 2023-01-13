using TimeIt.Configuration;

namespace TimeIt.Exporter;

public interface IExporter
{
    bool Enabled { get; }
    void SetConfiguration(Config configuration);
    void Export(object result);
}
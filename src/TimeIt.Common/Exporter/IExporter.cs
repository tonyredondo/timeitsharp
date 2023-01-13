using TimeIt.Common.Configuration;

namespace TimeIt.Common.Exporter;

public interface IExporter
{
    bool Enabled { get; }
    void SetConfiguration(Config configuration);
    void Export(object result);
}
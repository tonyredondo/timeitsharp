using System.Runtime.Loader;
using TimeIt;
using TimeIt.RuntimeMetrics;

class RuntimeMetricsInitializer
{
    private RuntimeMetricsWriter? _metricsWriter;

    public RuntimeMetricsInitializer()
    {
        if (Environment.GetEnvironmentVariable(Constants.TimeItMetricsTemporalPathEnvironmentVariable) is
            { Length: > 0 } metricsPath)
        {
            _metricsWriter = new RuntimeMetricsWriter(new FileStatsd(metricsPath), TimeSpan.FromMilliseconds(50));
            _metricsWriter.PushEvents();

            AppDomain.CurrentDomain.ProcessExit += (sender, args) =>
            {
                _metricsWriter.PushEvents();
            };
        }
    }
}
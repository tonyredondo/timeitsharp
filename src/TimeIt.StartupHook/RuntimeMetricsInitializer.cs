using TimeIt;
using TimeIt.RuntimeMetrics;

class RuntimeMetricsInitializer
{
    private readonly RuntimeMetricsWriter? MetricsWriter;

    public RuntimeMetricsInitializer(DateTime startDate)
    {
        if (Environment.GetEnvironmentVariable(Constants.TimeItMetricsTemporalPathEnvironmentVariable) is
            { Length: > 0 } metricsPath)
        {
            var fileStatsd = new FileStatsd(metricsPath);
            fileStatsd.Gauge(Constants.ProcessStartTimeUtcMetricName, startDate.ToBinary());
            MetricsWriter = new RuntimeMetricsWriter(fileStatsd, TimeSpan.FromMilliseconds(50));
            MetricsWriter.PushEvents();

            AppDomain.CurrentDomain.ProcessExit += (sender, args) =>
            {
                fileStatsd.Gauge(Constants.ProcessEndTimeUtcMetricName, Clock.UtcNow.ToBinary());
                MetricsWriter.PushEvents();
                fileStatsd.Dispose();
            };

            fileStatsd.Gauge(Constants.MainMethodStartTimeUtcMetricName, Clock.UtcNow.ToBinary());
        }
    }
}
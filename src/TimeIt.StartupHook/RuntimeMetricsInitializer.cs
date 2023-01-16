﻿using TimeIt;
using TimeIt.RuntimeMetrics;

class RuntimeMetricsInitializer
{
    private RuntimeMetricsWriter? _metricsWriter;

    public RuntimeMetricsInitializer(DateTime startDate)
    {
        if (Environment.GetEnvironmentVariable(Constants.TimeItMetricsTemporalPathEnvironmentVariable) is
            { Length: > 0 } metricsPath)
        {
            var fileStatsd = new FileStatsd(metricsPath);
            fileStatsd.Gauge(Constants.ProcessStartUtcMetricName, startDate.ToBinary());
            _metricsWriter = new RuntimeMetricsWriter(fileStatsd, TimeSpan.FromMilliseconds(50));
            _metricsWriter.PushEvents();

            AppDomain.CurrentDomain.ProcessExit += (sender, args) =>
            {
                _metricsWriter.PushEvents();
                fileStatsd.Dispose();
            };
        }
    }
}
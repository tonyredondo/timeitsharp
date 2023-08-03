public class StartupHook
{
    private static object? _runtimeMetrics;

    public static void Initialize()
    {
        var startDate = Clock.UtcNow;
        _runtimeMetrics = new RuntimeMetricsInitializer(startDate);
    }
}
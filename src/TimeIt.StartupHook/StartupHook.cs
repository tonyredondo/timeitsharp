using System.Reflection;
using System.Runtime.Loader;

public class StartupHook
{
    private static object? _runtimeMetrics;
    private static Assembly? _runtimeMetricsAssemblyCache;

    public static void Initialize()
    {
        var startDate = DateTime.UtcNow;
        AssemblyLoadContext.Default.Resolving += DefaultOnResolving;
        _runtimeMetrics = new RuntimeMetricsInitializer(startDate);
    }

    private static Assembly? DefaultOnResolving(AssemblyLoadContext ctx, AssemblyName assemblyName)
    {
        const string runtimeMetricsAssemblyName = "TimeIt.RuntimeMetrics";
        if (assemblyName.Name?.Equals(runtimeMetricsAssemblyName, StringComparison.Ordinal) == true)
        {
            if (_runtimeMetricsAssemblyCache is null)
            {
                var assemblyRuntimeMetricsPath = Path.Combine(Path.GetDirectoryName(typeof(StartupHook).Assembly.Location) ?? string.Empty, runtimeMetricsAssemblyName + ".dll");
                if (File.Exists(assemblyRuntimeMetricsPath))
                {
                    _runtimeMetricsAssemblyCache = ctx.LoadFromAssemblyPath(assemblyRuntimeMetricsPath);
                }
            }

            return _runtimeMetricsAssemblyCache;
        }

        return null;
    }
}
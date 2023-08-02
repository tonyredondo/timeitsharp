using System.Reflection;
using System.Runtime.Loader;

public class StartupHook
{
    private static object? _runtimeMetrics;
    private static Assembly? _runtimeMetricsAssemblyCache;
    private static string? _hookFolder;

    public static void Initialize()
    {
        var startDate = Clock.UtcNow;
        _hookFolder = Path.GetDirectoryName(typeof(StartupHook).Assembly.Location) ?? string.Empty;
        AssemblyLoadContext.Default.Resolving += DefaultOnResolving;
        _runtimeMetrics = new RuntimeMetricsInitializer(startDate);
    }

    private static Assembly? DefaultOnResolving(AssemblyLoadContext ctx, AssemblyName assemblyName)
    {
        if (_hookFolder is null)
        {
            return null;
        }

        const string runtimeMetricsAssemblyName = "TimeIt.RuntimeMetrics";
        if (assemblyName.Name?.Equals(runtimeMetricsAssemblyName, StringComparison.Ordinal) == true)
        {
            if (_runtimeMetricsAssemblyCache is null)
            {
                var assemblyRuntimeMetricsPath = Path.Combine(_hookFolder, runtimeMetricsAssemblyName + ".dll");
                if (File.Exists(assemblyRuntimeMetricsPath))
                {
                    _runtimeMetricsAssemblyCache = ctx.LoadFromAssemblyPath(assemblyRuntimeMetricsPath);
                }
            }

            return _runtimeMetricsAssemblyCache;
        }
        
        var otherAssemblies = Path.Combine(_hookFolder, assemblyName.Name + ".dll");
        if (File.Exists(otherAssemblies) && AssemblyName.GetAssemblyName(otherAssemblies).Version == assemblyName.Version)
        {
            return ctx.LoadFromAssemblyPath(otherAssemblies);
        }

        return null;
    }
}
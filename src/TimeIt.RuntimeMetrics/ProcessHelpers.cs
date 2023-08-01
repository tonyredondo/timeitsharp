using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace TimeIt.RuntimeMetrics;

// The following code is based on: https://github.com/DataDog/dd-trace-dotnet/blob/master/tracer/src/Datadog.Trace/RuntimeMetrics

public static class ProcessHelpers
{
    /// <summary>
    /// Wrapper around <see cref="Process.GetCurrentProcess"/> and its property accesses
    ///
    /// On .NET Framework the <see cref="Process"/> class is guarded by a
    /// LinkDemand for FullTrust, so partial trust callers will throw an exception.
    /// This exception is thrown when the caller method is being JIT compiled, NOT
    /// when Process.GetCurrentProcess is called, so this wrapper method allows
    /// us to catch the exception.
    /// </summary>
    /// <param name="userProcessorTime">CPU time in user mode</param>
    /// <param name="systemCpuTime">CPU time in kernel mode</param>
    /// <param name="totalProcessorTime">Total processor time</param>
    /// <param name="threadCount">Number of threads</param>
    /// <param name="privateMemorySize">Committed memory size</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void GetCurrentProcessRuntimeMetrics(
        out TimeSpan userProcessorTime,
        out TimeSpan systemCpuTime,
        out TimeSpan totalProcessorTime,
        out int threadCount,
        out long privateMemorySize)
    {
        using var process = Process.GetCurrentProcess();
        userProcessorTime = process.UserProcessorTime;
        systemCpuTime = process.PrivilegedProcessorTime;
        totalProcessorTime = process.TotalProcessorTime;
        threadCount = process.Threads.Count;
        privateMemorySize = process.PrivateMemorySize64;
    }
}
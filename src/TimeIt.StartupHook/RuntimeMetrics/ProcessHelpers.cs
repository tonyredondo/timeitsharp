using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace TimeIt.RuntimeMetrics;

// The following code is based on: https://github.com/DataDog/dd-trace-dotnet/blob/master/tracer/src/Datadog.Trace/RuntimeMetrics

public static class ProcessHelpers
{
    private static readonly Process CurrentProcess = Process.GetCurrentProcess();

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
        var process = CurrentProcess;
        userProcessorTime = process.UserProcessorTime;
        systemCpuTime = process.PrivilegedProcessorTime;
        threadCount = process.Threads.Count;
        privateMemorySize = process.PrivateMemorySize64;
        totalProcessorTime = systemCpuTime + userProcessorTime;
    }
}
using System.Diagnostics;
using Xunit;

namespace TimeItSharp.Cli.Tests;

public sealed class CliIntegrationTests
{
    [Fact]
    public async Task ExplicitCommandRejectsMixedPositionalValues()
    {
        var startInfo = CreateCliStartInfo();
        startInfo.ArgumentList.Add("--command");
        startInfo.ArgumentList.Add("echo");
        startInfo.ArgumentList.Add("positional");

        using var process = Process.Start(startInfo)!;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
        await process.WaitForExitAsync(timeout.Token);
        var output = await outputTask;
        var error = await errorTask;

        Assert.NotEqual(0, process.ExitCode);
        Assert.Contains("--command cannot be mixed with unnamed", output + error,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("values; put process arguments after --", output + error,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MissingCommandValueDoesNotConsumeCountOption()
    {
        var startInfo = CreateCliStartInfo();
        startInfo.ArgumentList.Add("--command");
        startInfo.ArgumentList.Add("--count");
        startInfo.ArgumentList.Add("1");

        using var process = Process.Start(startInfo)!;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
        await process.WaitForExitAsync(timeout.Token);
        var output = await outputTask;
        var error = await errorTask;

        Assert.NotEqual(0, process.ExitCode);
        Assert.Contains("--command", output + error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("requires exactly one value", output + error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MissingCommandValueDoesNotConsumeHelpOption()
    {
        var startInfo = CreateCliStartInfo();
        startInfo.ArgumentList.Add("--command");
        startInfo.ArgumentList.Add("--help");

        using var process = Process.Start(startInfo)!;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
        await process.WaitForExitAsync(timeout.Token);
        var output = await outputTask;
        var error = await errorTask;

        Assert.Equal(0, process.ExitCode);
        Assert.Contains("Usage", output + error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OptionTerminatorIgnoresAnAmbientSerializedExecutableFilename()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var directory = Path.Combine(Path.GetTempPath(), $"timeitsharp ambient argv {Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var executablePath = Path.Combine(directory, "capture");
        var ambientPath = Path.Combine(directory, "\"capture\" \"arg\"");
        var capturePath = Path.Combine(directory, "captured.txt");
        await File.WriteAllTextAsync(executablePath,
            "#!/bin/sh\nprintf 'actual:%s\n' \"$1\" > \"$TIMEITSHARP_CAPTURE\"\n");
        await File.WriteAllTextAsync(ambientPath,
            "#!/bin/sh\nprintf 'ambient\n' > \"$TIMEITSHARP_CAPTURE\"\n");
        File.SetUnixFileMode(executablePath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        File.SetUnixFileMode(ambientPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        try
        {
            var startInfo = CreateCliStartInfo();
            startInfo.WorkingDirectory = directory;
            startInfo.Environment["PATH"] = directory + Path.PathSeparator +
                Environment.GetEnvironmentVariable("PATH");
            startInfo.Environment["TIMEITSHARP_CAPTURE"] = capturePath;
            startInfo.ArgumentList.Add("--count");
            startInfo.ArgumentList.Add("1");
            startInfo.ArgumentList.Add("--warmup");
            startInfo.ArgumentList.Add("0");
            startInfo.ArgumentList.Add("--metrics");
            startInfo.ArgumentList.Add("false");
            startInfo.ArgumentList.Add("--");
            startInfo.ArgumentList.Add("capture");
            startInfo.ArgumentList.Add("arg");

            using var process = Process.Start(startInfo)!;
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));
            var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            var output = await outputTask;
            var error = await errorTask;

            Assert.True(process.ExitCode == 0,
                $"CLI exited with {process.ExitCode}. StdOut: {output} StdErr: {error}");
            Assert.Equal("actual:arg", (await File.ReadAllTextAsync(capturePath, timeout.Token)).Trim());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task OptionTerminatorPreservesRealProcessArgv()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var directory = Path.Combine(Path.GetTempPath(), $"timeitsharp argv {Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var scriptPath = Path.Combine(directory, "capture args.sh");
        var capturePath = Path.Combine(directory, "captured.txt");
        await File.WriteAllTextAsync(scriptPath, "#!/bin/sh\nprintf '%s\n' \"$@\" > \"$TIMEITSHARP_CAPTURE\"\n");
        File.SetUnixFileMode(scriptPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        try
        {
            var startInfo = CreateCliStartInfo();
            startInfo.ArgumentList.Add("--count");
            startInfo.ArgumentList.Add("1");
            startInfo.ArgumentList.Add("--warmup");
            startInfo.ArgumentList.Add("0");
            startInfo.ArgumentList.Add("--metrics");
            startInfo.ArgumentList.Add("false");
            startInfo.ArgumentList.Add("--");
            startInfo.ArgumentList.Add(scriptPath);
            startInfo.ArgumentList.Add("argument with spaces");
            startInfo.ArgumentList.Add("don't");
            startInfo.ArgumentList.Add(@"\\server\share\");
            startInfo.Environment["TIMEITSHARP_CAPTURE"] = capturePath;

            using var process = Process.Start(startInfo)!;
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));
            var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            var output = await outputTask;
            var error = await errorTask;

            Assert.True(process.ExitCode == 0,
                $"CLI exited with {process.ExitCode}. StdOut: {output} StdErr: {error}");
            Assert.Equal(new[] { "argument with spaces", "don't", @"\\server\share\" },
                await File.ReadAllLinesAsync(capturePath, timeout.Token));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
    private static ProcessStartInfo CreateCliStartInfo()
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(typeof(TimeItSharp.Cli.CliInputParser).Assembly.Location);
        return startInfo;
    }
}

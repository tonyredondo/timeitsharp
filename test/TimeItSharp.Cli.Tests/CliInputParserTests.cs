using TimeItSharp.Cli;
using Xunit;

namespace TimeItSharp.Cli.Tests;

public sealed class CliInputParserTests
{
    [Fact]
    public void JsonSuffixInCommandArgumentsDoesNotSelectConfigurationMode()
    {
        var input = CliInputParser.Classify("echo hello.json");

        Assert.Equal(CliInputKind.Command, input.Kind);
        var process = CliInputParser.ParseProcessCommand(input.Value);
        Assert.Equal("echo", process.ProcessName);
        Assert.Equal("hello.json", process.ProcessArguments);
    }

    [Fact]
    public void ConfigLikeSwitchInCommandArgumentsDoesNotSelectConfigurationMode()
    {
        var input = CliInputParser.Classify("echo --config foo.json");

        Assert.Equal(CliInputKind.Command, input.Kind);
        Assert.Equal("echo --config foo.json", input.Value);
    }

    [Fact]
    public void ExistingJsonFileIsConfiguration()
    {
        using var file = TemporaryFile.Create("{\"processName\":\"echo\"}", ".json");

        var input = CliInputParser.Classify(file.Path);

        Assert.Equal(CliInputKind.Configuration, input.Kind);
        Assert.Equal(file.Path, input.Value);
        Assert.False(input.IsExplicit);
    }

    [Fact]
    public void MissingJsonFileRemainsConfigurationInLegacyMode()
    {
        var path = Path.Combine(Path.GetTempPath(), $"timeitsharp-missing-{Guid.NewGuid():N}.JSON");

        var input = CliInputParser.Classify(path);

        Assert.Equal(CliInputKind.Configuration, input.Kind);
    }

    [Fact]
    public void ExplicitCommandCanUseJsonExecutableName()
    {
        var input = CliInputParser.Classify("does-not-need-to-exist.json", commandValue: "echo hello.json");

        Assert.Equal(CliInputKind.Command, input.Kind);
        Assert.True(input.IsExplicit);
        Assert.Equal("echo hello.json", input.Value);
    }

    [Fact]
    public void ExplicitConfigWinsOverCommandSuffixAndDoesNotRequireExistingFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"timeitsharp-explicit-{Guid.NewGuid():N}.json");

        var input = CliInputParser.Classify("echo hello.json", configurationPath: path);

        Assert.Equal(CliInputKind.Configuration, input.Kind);
        Assert.True(input.IsExplicit);
        Assert.Equal(path, input.Value);
    }

    [Fact]
    public void MultiplePositionalValuesCanBeJoinedAsACommand()
    {
        var value = CliInputParser.JoinCommandArguments(new[] { "echo", "hello.json" });

        Assert.Equal("echo hello.json", value);
        Assert.Equal(CliInputKind.Command, CliInputParser.Classify(value).Kind);
    }

    [Fact]
    public void OptionTerminatorPreservesAQuotedNonexistentPathToken()
    {
        var path = Path.Combine(Path.GetTempPath(), "timeitsharp missing executable");
        var normalized = CliInputParser.NormalizeArguments(new[] { "--", path, "--flag" });

        Assert.Equal($"--command=\"{path}\" --flag", normalized[0]);
        var command = CliInputParser.ParseProcessCommand(normalized[0]["--command=".Length..]);
        Assert.Equal(path, command.ProcessName);
        Assert.Equal("--flag", command.ProcessArguments);
    }

    [Fact]
    public void OptionTerminatorCanAppendArgumentsToACompleteCommandValue()
    {
        var normalized = CliInputParser.NormalizeArguments(new[] { "--", "echo hello.json", "--flag" });

        Assert.Equal(new[] { "--command=echo hello.json --flag" }, normalized);
    }

    [Fact]
    public void OptionTerminatorTurnsAllFollowingTokensIntoOneExplicitCommand()
    {
        var normalized = CliInputParser.NormalizeArguments(new[] { "--count", "1", "--", "echo", "hello.json" });

        Assert.Equal(new[] { "--count", "1", "--command=echo hello.json" }, normalized);
        var command = CliInputParser.Classify(normalized[2]["--command=".Length..], commandTerminated: true);
        Assert.Equal(CliInputKind.Command, command.Kind);
        Assert.Equal("echo hello.json", command.Value);
        Assert.True(command.IsExplicit);
    }

    [Fact]
    public void OptionTerminatorPreservesAQuotedCommandArgument()
    {
        var normalized = CliInputParser.NormalizeArguments(new[] { "--", "echo hello.json" });

        Assert.Equal(new[] { "--command=echo hello.json" }, normalized);
    }

    [Fact]
    public void OptionTerminatorCanPassAnOptionNamedProcessArgument()
    {
        var normalized = CliInputParser.NormalizeArguments(new[] { "--", "echo", "--config", "foo.json" });

        Assert.Equal(new[] { "--command=echo --config foo.json" }, normalized);
    }

    [Fact]
    public void ExistingExtensionlessJsonFileIsConfiguration()
    {
        using var file = TemporaryFile.Create("\ufeff {\"processName\":\"echo\"}", string.Empty);

        var input = CliInputParser.Classify(file.Path);

        Assert.Equal(CliInputKind.Configuration, input.Kind);
    }

    [Fact]
    public void UnterminatedQuotesAreRejectedBeforeProcessExecution()
    {
        var exception = Assert.Throws<ArgumentException>(() => CliInputParser.ParseProcessCommand("echo 'unterminated"));

        Assert.Contains("unterminated quote", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExistingExecutablePathWithSpacesCanBePassedAsOneExplicitCommandValue()
    {
        using var file = TemporaryFile.Create("#!/bin/sh\necho ok\n", string.Empty);

        var process = CliInputParser.ParseProcessCommand(file.Path);

        Assert.Equal(file.Path, process.ProcessName);
        Assert.Equal(string.Empty, process.ProcessArguments);
    }

    [Fact]
    public void QuotedExecutablePathWithSpacesIsParsed()
    {
        var process = CliInputParser.ParseProcessCommand("\"/tmp/my executable\" --version");

        Assert.Equal("/tmp/my executable", process.ProcessName);
        Assert.Equal("--version", process.ProcessArguments);
    }

    private sealed class TemporaryFile : IDisposable
    {
        private TemporaryFile(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TemporaryFile Create(string contents, string extension)
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"timeitsharp-cli-{Guid.NewGuid():N}{extension}");
            File.WriteAllText(path, contents);
            return new TemporaryFile(path);
        }

        public void Dispose()
        {
            try
            {
                File.Delete(Path);
            }
            catch (IOException)
            {
                // Best effort cleanup for a test fixture.
            }
        }
    }
}

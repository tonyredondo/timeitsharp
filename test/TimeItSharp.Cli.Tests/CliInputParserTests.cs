using TimeItSharp.Cli;
using Xunit;

namespace TimeItSharp.Cli.Tests;

public sealed class CliInputParserTests
{
    [Fact]
    public void JsonSuffixOnACompleteLegacyValueSelectsConfigurationMode()
    {
        var input = CliInputParser.Classify("echo path/hello.json");

        Assert.Equal(CliInputKind.Configuration, input.Kind);
        Assert.Equal("echo path/hello.json", input.Value);
    }

    [Fact]
    public void ExplicitCommandOverridesAJsonSuffixInCommandArguments()
    {
        var input = CliInputParser.Classify(null, commandValue: "echo --config foo.json");

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
    public void MissingBareRelativeJsonPathWithSpacesRemainsConfiguration()
    {
        var path = $"missing config {Guid.NewGuid():N}.json";

        var input = CliInputParser.Classify(path);

        Assert.Equal(CliInputKind.Configuration, input.Kind);
        Assert.Equal(path, input.Value);
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

        Assert.Equal("\"echo\" \"hello.json\"", value);
        Assert.Equal(CliInputKind.Command, CliInputParser.Classify(value).Kind);
    }

    [Fact]
    public void OptionTerminatorPreservesAQuotedNonexistentPathToken()
    {
        var path = Path.Combine(Path.GetTempPath(), "timeitsharp missing executable");
        var normalized = CliInputParser.NormalizeArguments(new[] { "--", path, "--flag" });

        Assert.Equal($"--command=\"{path}\" \"--flag\"", normalized[0]);
        var command = CliInputParser.ParseProcessCommand(normalized[0]["--command=".Length..]);
        Assert.Equal(path, command.ProcessName);
        Assert.Equal("\"--flag\"", command.ProcessArguments);
    }

    [Fact]
    public void OptionTerminatorPreservesEveryDiscreteArgumentBoundary()
    {
        var normalized = CliInputParser.NormalizeArguments(new[] { "--", "echo hello.json", "--flag" });

        Assert.Equal(new[] { "--command=\"echo hello.json\" \"--flag\"" }, normalized);
        var command = CliInputParser.ParseProcessCommand(normalized[0]["--command=".Length..]);
        Assert.Equal("echo hello.json", command.ProcessName);
        Assert.Equal("\"--flag\"", command.ProcessArguments);
    }

    [Fact]
    public void OptionTerminatorTurnsAllFollowingTokensIntoOneExplicitCommand()
    {
        var normalized = CliInputParser.NormalizeArguments(new[] { "--count", "1", "--", "echo", "hello.json" });

        Assert.Equal(new[] { "--count", "1", "--command=\"echo\" \"hello.json\"" }, normalized);
        var command = CliInputParser.Classify(normalized[2]["--command=".Length..], commandTerminated: true);
        Assert.Equal(CliInputKind.Command, command.Kind);
        Assert.Equal("\"echo\" \"hello.json\"", command.Value);
        Assert.True(command.IsExplicit);
    }

    [Fact]
    public void OptionTerminatorQuotesEvenSimpleArgvValues()
    {
        var normalized = CliInputParser.NormalizeArguments(new[] { "--", "echo", "hello", string.Empty });

        Assert.Equal(new[] { "--command=\"echo\" \"hello\" \"\"" }, normalized);
        var command = CliInputParser.ParseProcessCommand(normalized[0]["--command=".Length..]);
        Assert.Equal("echo", command.ProcessName);
        Assert.Equal("\"hello\" \"\"", command.ProcessArguments);
    }

    [Fact]
    public void OptionTerminatorPreservesAQuotedCommandArgument()
    {
        var normalized = CliInputParser.NormalizeArguments(new[] { "--", "echo hello.json" });

        Assert.Equal(new[] { "--command=\"echo hello.json\"" }, normalized);
    }

    [Fact]
    public void OptionTerminatorCanPassAnOptionNamedProcessArgument()
    {
        var normalized = CliInputParser.NormalizeArguments(new[] { "--", "echo", "--config", "foo.json" });

        Assert.Equal(new[] { "--command=\"echo\" \"--config\" \"foo.json\"" }, normalized);
    }

    [Fact]
    public void MissingAbsoluteJsonPathWithSpacesRemainsConfiguration()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            $"timeitsharp-missing-{Guid.NewGuid():N} config.json");

        var input = CliInputParser.Classify(path);

        Assert.Equal(CliInputKind.Configuration, input.Kind);
        Assert.Equal(path, input.Value);
    }

    [Fact]
    public void MissingRelativeJsonPathWithDirectoryAndSpacesRemainsConfiguration()
    {
        var path = Path.Combine("missing directory", $"config {Guid.NewGuid():N}.json");

        var input = CliInputParser.Classify(path);

        Assert.Equal(CliInputKind.Configuration, input.Kind);
        Assert.Equal(path, input.Value);
    }

    [Fact]
    public void OptionTerminatorDoesNotDependOnWhetherACombinedExecutableExists()
    {
        var firstToken = Path.Combine(Path.GetTempPath(), $"timeitsharp-{Guid.NewGuid():N}-echo");
        var combinedPath = firstToken + " hello";
        File.WriteAllText(combinedPath, string.Empty);
        try
        {
            var normalized = CliInputParser.NormalizeArguments(new[] { "--", firstToken, "hello" });
            var command = CliInputParser.ParseProcessCommand(normalized[0]["--command=".Length..]);

            Assert.Equal($"--command=\"{firstToken}\" \"hello\"", normalized[0]);
            Assert.Equal(firstToken, command.ProcessName);
            Assert.Equal("\"hello\"", command.ProcessArguments);
        }
        finally
        {
            File.Delete(combinedPath);
        }
    }

    [Fact]
    public void LegacyPathCommandIgnoresAnAmbientCombinedSimpleFilename()
    {
        var executable = $"capture-{Guid.NewGuid():N}";
        var combinedFilename = $"{executable} arg";
        File.WriteAllText(combinedFilename, string.Empty);
        try
        {
            var process = CliInputParser.ParseProcessCommand(combinedFilename);

            Assert.Equal(executable, process.ProcessName);
            Assert.Equal("arg", process.ProcessArguments);
        }
        finally
        {
            File.Delete(combinedFilename);
        }
    }

    [Fact]
    public void ExplicitExecutablePathWithSpacesStillUsesLongestExistingPrefix()
    {
        var executable = Path.Combine(Path.GetTempPath(),
            $"timeitsharp-{Guid.NewGuid():N} executable with spaces");
        File.WriteAllText(executable, string.Empty);
        try
        {
            var process = CliInputParser.ParseProcessCommand($"{executable} --version");

            Assert.Equal(executable, process.ProcessName);
            Assert.Equal("--version", process.ProcessArguments);
        }
        finally
        {
            File.Delete(executable);
        }
    }

    [Fact]
    public void QuotedArgvSerializationIgnoresAnAmbientCompleteFilename()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var executable = $"capture-{Guid.NewGuid():N}";
        var commandLine = $"\"{executable}\" \"arg\"";
        File.WriteAllText(commandLine, string.Empty);
        try
        {
            var process = CliInputParser.ParseProcessCommand(commandLine);

            Assert.Equal(executable, process.ProcessName);
            Assert.Equal("\"arg\"", process.ProcessArguments);
        }
        finally
        {
            File.Delete(commandLine);
        }
    }

    [Fact]
    public void QuotedArgvSerializationIgnoresAnAmbientPrefixFilename()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var executable = $"capture-{Guid.NewGuid():N}";
        var ambientPrefix = $"\"{executable}\"";
        File.WriteAllText(ambientPrefix, string.Empty);
        try
        {
            var commandLine = $"{ambientPrefix} \"arg\"";
            var process = CliInputParser.ParseProcessCommand(commandLine);

            Assert.Equal(executable, process.ProcessName);
            Assert.Equal("\"arg\"", process.ProcessArguments);
        }
        finally
        {
            File.Delete(ambientPrefix);
        }
    }

    [Fact]
    public void ExistingExtensionlessJsonFileIsConfiguration()
    {
        using var file = TemporaryFile.Create("\ufeff {\"processName\":\"echo\"}", string.Empty);

        var input = CliInputParser.Classify(file.Path);

        Assert.Equal(CliInputKind.Configuration, input.Kind);
    }

    [Fact]
    public void PosixQuoteConcatenationPreservesAnEmbeddedApostrophe()
    {
        var process = CliInputParser.ParseProcessCommand("'foo'\"'\"'bar'");

        Assert.Equal("foo'bar", process.ProcessName);
        Assert.Equal(string.Empty, process.ProcessArguments);
    }

    [Fact]
    public void EmbeddedApostrophesAroundWhitespaceRemainLiteral()
    {
        var process = CliInputParser.ParseProcessCommand("echo foo'bar baz'");

        Assert.Equal("echo", process.ProcessName);
        Assert.Equal("foo'bar baz'", process.ProcessArguments);
    }

    [Fact]
    public void TrailingBackslashInsideSingleQuotesIsPreserved()
    {
        var process = CliInputParser.ParseProcessCommand("echo 'C:\\temp\\'");

        Assert.Equal("echo", process.ProcessName);
        Assert.Equal("\"C:\\temp\\\\\"", process.ProcessArguments);
    }

    [Fact]
    public void EmbeddedApostrophesWithAnUnquotedSuffixRemainLiteral()
    {
        var process = CliInputParser.ParseProcessCommand("echo foo'bar baz'qux");

        Assert.Equal("echo", process.ProcessName);
        Assert.Equal("foo'bar baz'qux", process.ProcessArguments);
    }

    [Fact]
    public void ContractionBeforeLaterQuotedTokenRemainsLiteral()
    {
        var process = CliInputParser.ParseProcessCommand("echo don't say 'hi'");

        Assert.Equal("echo", process.ProcessName);
        Assert.Equal("don't say \"hi\"", process.ProcessArguments);
    }

    [Fact]
    public void PairedApostrophesInANameRemainLiteral()
    {
        var process = CliInputParser.ParseProcessCommand("echo O'Brien's");

        Assert.Equal("O'Brien's", process.ProcessArguments);
    }

    [Fact]
    public void UnquotedUncExecutableKeepsItsLeadingBackslashes()
    {
        var process = CliInputParser.ParseProcessCommand(@"\\server\share\tool.exe --version");

        Assert.Equal(@"\\server\share\tool.exe", process.ProcessName);
        Assert.Equal("--version", process.ProcessArguments);
    }

    [Fact]
    public void QuotedUncExecutableKeepsDoubledAndTrailingBackslashes()
    {
        var process = CliInputParser.ParseProcessCommand("\"\\\\server\\share name\\app.exe\" --version");

        Assert.Equal(@"\\server\share name\app.exe", process.ProcessName);
        Assert.Equal("--version", process.ProcessArguments);
    }

    [Fact]
    public void AdjacentContractionsAndPossessivesRemainLiteral()
    {
        var contractions = CliInputParser.ParseProcessCommand("echo don't won't");
        var possessives = CliInputParser.ParseProcessCommand("echo James' team Chris' work");

        Assert.Equal("don't won't", contractions.ProcessArguments);
        Assert.Equal("James' team Chris' work", possessives.ProcessArguments);
    }

    [Fact]
    public void PosixQuoteConcatenationWorksInProcessArguments()
    {
        var process = CliInputParser.ParseProcessCommand("echo 'foo'\"'\"'bar'");

        Assert.Equal("echo", process.ProcessName);
        Assert.Equal("\"foo'bar\"", process.ProcessArguments);
    }

    [Fact]
    public void EvenBackslashesBeforeAQuoteOpenAQuotedSpan()
    {
        var commandLine = "foo" + new string('\\', 2) + "\"bar\"";

        var process = CliInputParser.ParseProcessCommand(commandLine);

        Assert.Equal("foo\\bar", process.ProcessName);
    }

    [Fact]
    public void OddBackslashesBeforeAQuoteProduceALiteralQuote()
    {
        var commandLine = "foo" + new string('\\', 3) + "\"bar";

        var process = CliInputParser.ParseProcessCommand(commandLine);

        Assert.Equal("foo\\\"bar", process.ProcessName);
    }

    [Fact]
    public void EscapedApostropheCanAppearInExecutableName()
    {
        var process = CliInputParser.ParseProcessCommand("foo\\'bar");

        Assert.Equal("foo'bar", process.ProcessName);
    }

    [Fact]
    public void EmptyQuotedExecutableIsRejected()
    {
        Assert.Throws<ArgumentException>(() => CliInputParser.ParseProcessCommand("''"));
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

    [Fact]
    public void OptionTerminatorAppendsToAnExistingCommandOption()
    {
        var normalized = CliInputParser.NormalizeArguments(new[] { "--command", "echo", "--", "a b", "c" });

        Assert.Equal(new[] { "--command=echo \"a b\" \"c\"" }, normalized);
    }

    [Fact]
    public void MissingCommandValueDoesNotConsumeTheNextOption()
    {
        var normalized = CliInputParser.NormalizeArguments(new[] { "--command", "--count", "1", "--", "app" });

        Assert.Equal(new[] { "--command=\"app\"", "--count", "1" }, normalized);
    }

    [Fact]
    public void PosixEscapedSpaceIsPreservedInCommandArguments()
    {
        var process = CliInputParser.ParseProcessCommand("echo a\\ b");

        Assert.Equal("a\\ b", process.ProcessArguments);
    }

    [Fact]
    public void EscapedDoubleQuoteIsNotTreatedAsAnOpeningQuote()
    {
        var process = CliInputParser.ParseProcessCommand("echo a\\\"b");

        Assert.Equal("a\\\"b", process.ProcessArguments);
    }

    [Fact]
    public void EmbeddedApostrophesInAnUnquotedTokenRemainLiteral()
    {
        var process = CliInputParser.ParseProcessCommand("echo foo'bar baz'");

        Assert.Equal("foo'bar baz'", process.ProcessArguments);
    }

    [Fact]
    public void ContractionDoesNotConsumeALaterQuotedToken()
    {
        var process = CliInputParser.ParseProcessCommand("echo don't say 'hi'");

        Assert.Equal("don't say \"hi\"", process.ProcessArguments);
    }

    [Fact]
    public void UnterminatedAssignmentQuoteIsRejected()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            CliInputParser.ParseProcessCommand("echo key='unterminated"));

        Assert.Contains("unterminated quote", exception.Message, StringComparison.OrdinalIgnoreCase);
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

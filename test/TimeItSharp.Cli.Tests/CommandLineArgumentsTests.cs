using System.Reflection;
using TimeItSharp.Common;
using Xunit;

namespace TimeItSharp.Cli.Tests;

public sealed class CommandLineArgumentsTests
{
    [Fact]
    public void LiteralApostrophesAreNotReinterpretedAsQuotes()
    {
        var values = Parse("don't O'Brien's 'two words'");

        Assert.Equal(new[] { "don't", "O'Brien's", "two words" }, values);
    }

    [Fact]
    public void AdjacentContractionsAndPossessivesRemainSeparate()
    {
        var values = Parse("don't won't James' team Chris' work");

        Assert.Equal(new[] { "don't", "won't", "James'", "team", "Chris'", "work" }, values);
    }

    [Fact]
    public void UncAndDoubledBackslashesRemainLiteral()
    {
        var values = Parse(@"\\server\share plain\\value C:\temp\");

        Assert.Equal(new[] { @"\\server\share", @"plain\\value", @"C:\temp\" }, values);
    }

    [Fact]
    public void ABackslashBeforeWhitespaceDoesNotMergeArguments()
    {
        var values = Parse(@"C:\temp\ next");

        Assert.Equal(new[] { @"C:\temp\", "next" }, values);
    }

    [Fact]
    public void QuotedUncPathsAndDoubledBackslashesArePreserved()
    {
        var values = Parse("\"\\\\server\\share name\\app.exe\" \"a\\\\b\"");

        Assert.Equal(new[] { @"\\server\share name\app.exe", @"a\\b" }, values);
    }

    [Fact]
    public void QuotedTrailingBackslashesFollowCommandLineEscapingRules()
    {
        var values = Parse("\"\\\\server\\share\\folder\\\\\"");

        Assert.Equal(new[] { @"\\server\share\folder\" }, values);
    }

    private static IReadOnlyList<string> Parse(string text)
    {
        var type = typeof(TimeItEngine).Assembly.GetType("TimeItSharp.Common.CommandLineArguments", throwOnError: true)!;
        var method = type.GetMethod("Parse", BindingFlags.Static | BindingFlags.NonPublic)!;
        return (IReadOnlyList<string>)method.Invoke(null, new object?[] { text })!;
    }
}

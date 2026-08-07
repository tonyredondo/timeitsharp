using System.Text.Json;
using TimeItSharp.Common.Configuration;

namespace TimeItSharp.Common;

/// <summary>
/// Compares extension declarations without accidentally collapsing distinct assemblies or options.
/// </summary>
internal static class ExtensionIdentity
{
    public static bool AreEquivalent(
        AssemblyLoadInfo left,
        AssemblyLoadInfo right,
        Type extensionContract,
        string? baseDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        ArgumentNullException.ThrowIfNull(extensionContract);

        if (!OptionsAreEqual(left.Options, right.Options))
        {
            return false;
        }

        if (left.InMemoryType is not null || right.InMemoryType is not null)
        {
            if (left.InMemoryType is { } leftInMemory && right.InMemoryType is { } rightInMemory)
            {
                return leftInMemory == rightInMemory;
            }

            var inMemoryType = left.InMemoryType ?? right.InMemoryType!;
            var declaration = left.InMemoryType is not null ? right : left;
            return MatchesType(declaration, inMemoryType, extensionContract, baseDirectory);
        }

        if (!string.IsNullOrWhiteSpace(left.Name) || !string.IsNullOrWhiteSpace(right.Name))
        {
            if (string.IsNullOrWhiteSpace(left.Name) || string.IsNullOrWhiteSpace(right.Name))
            {
                // A built-in name and an equivalent file declaration identify the same extension,
                // provided the file is the assembly that defines that built-in type. A custom name
                // is never guessed from a type/path pair.
                if (!string.IsNullOrWhiteSpace(left.Name) &&
                    BuiltInExtensionAliases.TryResolve(extensionContract, left.Name, out var leftNameType))
                {
                    return MatchesType(right, leftNameType, extensionContract, baseDirectory);
                }

                if (!string.IsNullOrWhiteSpace(right.Name) &&
                    BuiltInExtensionAliases.TryResolve(extensionContract, right.Name, out var rightNameType))
                {
                    return MatchesType(left, rightNameType, extensionContract, baseDirectory);
                }

                return false;
            }

            if (BuiltInExtensionAliases.TryResolve(extensionContract, left.Name, out var leftBuiltIn) &&
                BuiltInExtensionAliases.TryResolve(extensionContract, right.Name, out var rightBuiltIn))
            {
                return leftBuiltIn == rightBuiltIn;
            }

            return string.Equals(left.Name, right.Name, StringComparison.Ordinal);
        }

        // FilePath + Type is the only non-in-memory selector that reaches this point. Compare the
        // path as well as the type: two assemblies may expose the same full type name.
        return string.Equals(left.Type, right.Type, StringComparison.Ordinal) &&
               PathsEqual(left.FilePath, right.FilePath, baseDirectory);
    }

    public static bool MatchesType(
        AssemblyLoadInfo declaration,
        Type extensionType,
        Type extensionContract,
        string? baseDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        ArgumentNullException.ThrowIfNull(extensionType);
        ArgumentNullException.ThrowIfNull(extensionContract);

        if (!extensionContract.IsAssignableFrom(extensionType))
        {
            return false;
        }

        if (declaration.InMemoryType == extensionType ||
            BuiltInExtensionAliases.IsAlias(extensionType, declaration.Name))
        {
            return true;
        }

        if (!string.Equals(declaration.Type, extensionType.FullName, StringComparison.Ordinal))
        {
            return false;
        }

        // A declaration loaded from a file must identify that file too. This prevents a type with
        // the same full name in another assembly from being treated as the same extension.
        var extensionAssemblyPath = extensionType.Assembly.Location;
        return PathsEqual(declaration.FilePath, extensionAssemblyPath, baseDirectory);
    }

    public static string NormalizePath(string path, string? baseDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A non-empty path is required.", nameof(path));
        }

        var candidate = path;
        if (!Path.IsPathRooted(candidate) && !string.IsNullOrWhiteSpace(baseDirectory))
        {
            candidate = Path.Combine(baseDirectory, candidate);
        }

        try
        {
            return Path.GetFullPath(candidate);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // Validation/resolution reports the original path later; keeping this helper pure
            // makes duplicate detection deterministic even for malformed input.
            return candidate;
        }
    }

    private static bool PathsEqual(string? left, string? right, string? baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return string.IsNullOrWhiteSpace(left) && string.IsNullOrWhiteSpace(right);
        }

        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(NormalizePath(left, baseDirectory), NormalizePath(right, baseDirectory), comparison);
    }

    private static bool OptionsAreEqual(
        IReadOnlyDictionary<string, JsonElement?>? left,
        IReadOnlyDictionary<string, JsonElement?>? right)
    {
        if (left is null || left.Count == 0)
        {
            return right is null || right.Count == 0;
        }

        if (right is null || left.Count != right.Count)
        {
            return false;
        }

        foreach (var pair in left)
        {
            if (!right.TryGetValue(pair.Key, out var rightValue) || !JsonValuesAreEqual(pair.Value, rightValue))
            {
                return false;
            }
        }

        return true;
    }

    private static bool JsonValuesAreEqual(JsonElement? left, JsonElement? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        // GetRawText is stable for values produced by System.Text.Json and, importantly, does not
        // equate values whose options differ merely because they were deserialized from distinct
        // JsonDocuments. A default JsonElement has no raw text, but still has a well-defined
        // undefined value for identity purposes.
        if (left.Value.ValueKind != right.Value.ValueKind)
        {
            return false;
        }

        if (left.Value.ValueKind == JsonValueKind.Undefined)
        {
            return true;
        }

        return string.Equals(left.Value.GetRawText(), right.Value.GetRawText(), StringComparison.Ordinal);
    }
}

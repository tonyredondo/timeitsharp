using TimeItSharp.Common.Assertors;
using TimeItSharp.Common.Exporters;
using TimeItSharp.Common.Services;

namespace TimeItSharp.Common;

/// <summary>
/// The aliases understood for extensions shipped with TimeItSharp.
/// </summary>
/// <remarks>
/// Keep this table as the single source of truth for fluent configuration and runtime
/// resolution. Custom extension names remain case-sensitive; built-in aliases are deliberately
/// case-insensitive for compatibility with JSON and command-line configuration.
/// </remarks>
public static class BuiltInExtensionAliases
{
    private static readonly IReadOnlyDictionary<Type, string[]> Aliases =
        new Dictionary<Type, string[]>
        {
            [typeof(ConsoleExporter)] = new[] { "Console", "ConsoleExporter" },
            [typeof(JsonExporter)] = new[] { "Json", "JsonExporter" },
            [typeof(DatadogExporter)] = new[] { "Datadog", "DatadogExporter" },
            [typeof(DefaultAssertor)] = new[] { "Default", "DefaultAssertor" },
            [typeof(NoopService)] = new[] { "Noop", "NoopService" },
            [typeof(DatadogProfilerService)] = new[] { "DatadogProfiler", "DatadogProfilerService" },
        };

    /// <summary>
    /// Gets the aliases for a built-in extension type.
    /// </summary>
    public static IReadOnlyList<string> GetAliases(Type extensionType)
    {
        ArgumentNullException.ThrowIfNull(extensionType);
        return Aliases.TryGetValue(extensionType, out var aliases)
            ? aliases.ToArray()
            : Array.Empty<string>();
    }

    /// <summary>
    /// Determines whether <paramref name="name"/> is an alias for
    /// <paramref name="extensionType"/>.
    /// </summary>
    public static bool IsAlias(Type extensionType, string? name)
    {
        ArgumentNullException.ThrowIfNull(extensionType);
        return !string.IsNullOrWhiteSpace(name) &&
               Aliases.TryGetValue(extensionType, out var aliases) &&
               aliases.Any(alias => string.Equals(alias, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Resolves a built-in alias for an extension contract.
    /// </summary>
    public static bool TryResolve(Type extensionContract, string? name, out Type extensionType)
    {
        ArgumentNullException.ThrowIfNull(extensionContract);
        extensionType = null!;
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        foreach (var pair in Aliases)
        {
            if (extensionContract.IsAssignableFrom(pair.Key) &&
                pair.Value.Any(alias => string.Equals(alias, name, StringComparison.OrdinalIgnoreCase)))
            {
                extensionType = pair.Key;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns the canonical display name for a built-in type.
    /// </summary>
    public static string GetCanonicalName(Type extensionType)
    {
        ArgumentNullException.ThrowIfNull(extensionType);
        if (extensionType == typeof(DatadogExporter))
        {
            return "Datadog";
        }

        if (extensionType == typeof(DatadogProfilerService))
        {
            return "DatadogProfiler";
        }

        return extensionType.Name;
    }
}

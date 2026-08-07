using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Loader;
using TimeItSharp.Common.Configuration;

namespace TimeItSharp.Common;

/// <summary>
/// Resolves extension declarations consistently for the engine and configuration builder.
/// </summary>
internal static class ExtensionResolver
{
    [RequiresUnreferencedCode("Loads extension types by name or from an assembly path.")]
    public static List<(T Instance, AssemblyLoadInfo? LoadInfo)> Resolve<T>(
        IReadOnlyList<AssemblyLoadInfo>? assemblyLoadInfos,
        Func<List<T>>? defaultListFunc = null,
        string? baseDirectory = null)
        where T : class, INamedExtension
    {
        if (assemblyLoadInfos is null || assemblyLoadInfos.Count == 0)
        {
            return (defaultListFunc?.Invoke() ?? new List<T>())
                .Select(instance =>
                {
                    if (instance is null)
                    {
                        throw new InvalidOperationException($"The default {typeof(T).Name} extension list contains a null entry.");
                    }

                    return (instance, (AssemblyLoadInfo?)null);
                })
                .ToList();
        }

        var result = new List<(T Instance, AssemblyLoadInfo? LoadInfo)>();
        var loadContext = AssemblyLoadContext.Default;
        for (var index = 0; index < assemblyLoadInfos.Count; index++)
        {
            var loadInfo = assemblyLoadInfos[index];
            if (loadInfo is null)
            {
                throw new InvalidOperationException($"{typeof(T).Name} extension entry at index {index} cannot be null.");
            }

            ValidateSelector<T>(loadInfo, index);
            try
            {
                var instance = ResolveOne<T>(loadInfo, loadContext, baseDirectory);
                result.Add((instance, loadInfo));
            }
            catch (Exception ex) when (ex is not InvalidOperationException &&
                                       ex is not FileNotFoundException)
            {
                throw new InvalidOperationException(
                    $"Could not load {typeof(T).Name} extension at index {index} " +
                    $"('{loadInfo.Name ?? loadInfo.Type ?? loadInfo.FilePath}').",
                    ex);
            }
        }

        return result;
    }

    internal static void ValidateSelector<T>(AssemblyLoadInfo info, int index)
        where T : class, INamedExtension
    {
        ArgumentNullException.ThrowIfNull(info);
        var errors = Config.GetAssemblyLoadInfoValidationErrors(
            info,
            $"{GetPropertyName(typeof(T))}[{index}]");
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join("; ", errors));
        }
    }

    [RequiresUnreferencedCode("Loads extension types by name or from an assembly path.")]
    private static T ResolveOne<T>(
        AssemblyLoadInfo loadInfo,
        AssemblyLoadContext loadContext,
        string? baseDirectory)
        where T : class, INamedExtension
    {
        if (loadInfo.InMemoryType is { } inMemoryType)
        {
            return CreateInstance<T>(inMemoryType, loadInfo, "in-memory type");
        }

        if (!string.IsNullOrWhiteSpace(loadInfo.Name) &&
            BuiltInExtensionAliases.TryResolve(typeof(T), loadInfo.Name, out var builtInType))
        {
            return CreateInstance<T>(builtInType, loadInfo, $"built-in alias '{loadInfo.Name}'");
        }

        if (!string.IsNullOrWhiteSpace(loadInfo.FilePath))
        {
            var assemblyPath = ExtensionIdentity.NormalizePath(loadInfo.FilePath, baseDirectory);
            if (!File.Exists(assemblyPath))
            {
                throw new FileNotFoundException("Extension assembly not found.", assemblyPath);
            }

            var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);
            var extensionType = assembly.GetType(loadInfo.Type!, throwOnError: true);
            return CreateInstance<T>(extensionType!, loadInfo, $"'{assemblyPath}'");
        }

        // Name resolution for custom extensions is intentionally exact. Built-in aliases are
        // handled above and are case-insensitive by contract.
        var activationError = default(Exception);
        foreach (var assembly in loadContext.Assemblies)
        {
            TypeInfo[] definedTypes;
            try
            {
                definedTypes = assembly.DefinedTypes.ToArray();
            }
            catch (ReflectionTypeLoadException ex)
            {
                definedTypes = ex.Types
                    .Where(type => type is not null)
                    .Select(type => type!.GetTypeInfo())
                    .ToArray();
            }

            foreach (var typeInfo in definedTypes)
            {
                if (typeInfo.IsAbstract || typeInfo.IsInterface || typeInfo.IsEnum ||
                    !typeof(T).IsAssignableFrom(typeInfo.AsType()))
                {
                    continue;
                }

                try
                {
                    if (Activator.CreateInstance(typeInfo.AsType()) is T candidate &&
                        string.Equals(candidate.Name, loadInfo.Name, StringComparison.Ordinal))
                    {
                        return candidate;
                    }
                }
                catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException)
                {
                    // A constructor failure in an unrelated extension must not prevent an exact
                    // name match later in the scan.  The CLR type name is the only non-activating
                    // hint available here; retain a diagnostic only when it matches the selector.
                    if (string.Equals(typeInfo.Name, loadInfo.Name, StringComparison.Ordinal))
                    {
                        activationError ??= ex;
                    }
                }
            }
        }

        if (activationError is not null)
        {
            throw new InvalidOperationException(
                $"Could not create {typeof(T).Name} extension named '{loadInfo.Name}'.",
                activationError);
        }

        throw new InvalidOperationException(
            $"Could not find {typeof(T).Name} extension named '{loadInfo.Name}'.");
    }

    [RequiresUnreferencedCode("Creates extension instances through reflection.")]
    private static T CreateInstance<T>(Type extensionType, AssemblyLoadInfo loadInfo, string source)
        where T : class, INamedExtension
    {
        if (!typeof(T).IsAssignableFrom(extensionType))
        {
            throw new InvalidOperationException(
                $"Type '{extensionType.FullName ?? extensionType.Name}' from {source} does not implement {typeof(T).FullName}.");
        }

        if (extensionType.IsAbstract || extensionType.IsInterface || extensionType.IsEnum)
        {
            throw new InvalidOperationException(
                $"Type '{extensionType.FullName ?? extensionType.Name}' from {source} cannot be instantiated.");
        }

        if (Activator.CreateInstance(extensionType) is not T instance)
        {
            throw new InvalidOperationException(
                $"Could not create {typeof(T).Name} extension '{loadInfo.Name ?? loadInfo.Type ?? loadInfo.FilePath}'.");
        }

        return instance;
    }

    private static string GetPropertyName(Type extensionContract)
    {
        if (extensionContract == typeof(Exporters.IExporter))
        {
            return "exporters";
        }

        if (extensionContract == typeof(Assertors.IAssertor))
        {
            return "assertors";
        }

        if (extensionContract == typeof(Services.IService))
        {
            return "services";
        }

        return "extensions";
    }

}

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
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
        // Do not run constructors for every assignable extension merely to discover its Name.
        // Probe the property on an uninitialized instance first, then activate only exact matches.
        // Constructors remain the authoritative path for the selected extension and can still
        // report their own failure without causing side effects in unrelated extensions.
        var matchingTypes = new List<Type>();
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
                var type = typeInfo.AsType();
                if (typeInfo.IsAbstract || typeInfo.IsInterface || typeInfo.IsEnum ||
                    !typeof(T).IsAssignableFrom(type))
                {
                    continue;
                }

                if (string.Equals(typeInfo.Name, loadInfo.Name, StringComparison.Ordinal) ||
                    string.Equals(typeInfo.FullName, loadInfo.Name, StringComparison.Ordinal))
                {
                    matchingTypes.Add(type);
                    continue;
                }

                try
                {
                    if (RuntimeHelpers.GetUninitializedObject(type) is T probe &&
                        string.Equals(probe.Name, loadInfo.Name, StringComparison.Ordinal))
                    {
                        matchingTypes.Add(type);
                    }
                }
                catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
                {
                    // A custom Name getter may depend on constructor state. Such an extension
                    // must use FilePath+Type (or an in-memory type) rather than causing unrelated
                    // constructors to run during name discovery.
                }
            }
        }

        Exception? activationError = null;
        foreach (var matchingType in matchingTypes.Distinct())
        {
            try
            {
                return CreateInstance<T>(matchingType, loadInfo, $"custom name '{loadInfo.Name}'");
            }
            catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
            {
                activationError ??= ex;
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

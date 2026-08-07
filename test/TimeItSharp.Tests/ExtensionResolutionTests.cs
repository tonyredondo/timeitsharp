using System.Text.Json;
using TimeItSharp.Common.Assertors;
using TimeItSharp.Common.Configuration;
using TimeItSharp.Common.Configuration.Builder;
using TimeItSharp.Common.Exporters;
using TimeItSharp.Common.Services;

namespace TimeItSharp.Tests;

public sealed class ExtensionResolutionTests
{
    [Theory]
    [InlineData(typeof(IExporter), "Console", typeof(ConsoleExporter))]
    [InlineData(typeof(IExporter), "consoleexporter", typeof(ConsoleExporter))]
    [InlineData(typeof(IExporter), "Json", typeof(JsonExporter))]
    [InlineData(typeof(IExporter), "JsonExporter", typeof(JsonExporter))]
    [InlineData(typeof(IExporter), "Datadog", typeof(DatadogExporter))]
    [InlineData(typeof(IExporter), "DatadogExporter", typeof(DatadogExporter))]
    [InlineData(typeof(IAssertor), "DefaultAssertor", typeof(DefaultAssertor))]
    [InlineData(typeof(IAssertor), "Default", typeof(DefaultAssertor))]
    [InlineData(typeof(IService), "NoopService", typeof(NoopService))]
    [InlineData(typeof(IService), "DatadogProfiler", typeof(DatadogProfilerService))]
    public void Built_in_aliases_are_shared_by_contract(
        Type contract,
        string alias,
        Type expectedType)
    {
        Assert.True(BuiltInExtensionAliases.TryResolve(contract, alias, out var actualType));
        Assert.Equal(expectedType, actualType);
    }

    [Fact]
    public void Resolver_loads_built_in_alias_without_scanning_or_constructor_name_matching()
    {
        var exporter = Assert.Single(
            ExtensionResolver.Resolve<IExporter>(
                new[] { new AssemblyLoadInfo { Name = "jSoN" } }));

        Assert.IsType<JsonExporter>(exporter.Instance);
    }

    [Fact]
    public void Resolver_resolves_relative_file_paths_against_configuration_directory()
    {
        var assemblyPath = typeof(JsonExporter).Assembly.Location;
        var baseDirectory = Path.GetDirectoryName(assemblyPath)!;
        var declaration = new AssemblyLoadInfo
        {
            FilePath = Path.GetFileName(assemblyPath),
            Type = typeof(JsonExporter).FullName,
        };

        var exporter = Assert.Single(
            ExtensionResolver.Resolve<IExporter>(new[] { declaration }, baseDirectory: baseDirectory));

        Assert.IsType<JsonExporter>(exporter.Instance);
    }

    [Fact]
    public void Resolver_rejects_null_and_ambiguous_entries_with_indices()
    {
        var nullException = Assert.Throws<InvalidOperationException>(() =>
            ExtensionResolver.Resolve<IExporter>(new AssemblyLoadInfo[] { null! }));
        Assert.Contains("index 0", nullException.Message);

        var ambiguousException = Assert.Throws<InvalidOperationException>(() =>
            ExtensionResolver.Resolve<IExporter>(new[]
            {
                new AssemblyLoadInfo { Name = "Json", FilePath = "some.dll", Type = typeof(JsonExporter).FullName },
            }));
        Assert.Contains("cannot be combined", ambiguousException.Message);
    }

    [Fact]
    public void Config_validation_rejects_selector_combinations_and_type_only_entries()
    {
        var config = new Config
        {
            Count = 1,
            EnableMetrics = false,
            ProcessName = "echo",
        };
        config.Scenarios.Add(new Scenario { Name = "scenario" });
        config.Exporters.Add(new AssemblyLoadInfo { Type = typeof(JsonExporter).FullName });
        config.Assertors.Add(new AssemblyLoadInfo { Name = "Default", Type = typeof(DefaultAssertor).FullName });
        config.Services.Add(new AssemblyLoadInfo { FilePath = "extension.dll" });

        Assert.False(config.TryValidate(out var errors));
        Assert.Contains(errors, error => error.Contains("exporters[0].filePath is required", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("assertors[0] name cannot be combined", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("services[0].type is required", StringComparison.Ordinal));
    }

    [Fact]
    public void Builder_throws_for_null_config_and_null_arguments_instead_of_silently_ignoring_them()
    {
        Assert.Throws<ArgumentNullException>(() => new ConfigBuilder(null!));

        var builder = ConfigBuilder.Create();
        Assert.Throws<ArgumentNullException>(() => builder.WithExporter((AssemblyLoadInfo)null!));
        Assert.Throws<ArgumentNullException>(() => builder.WithExporter((AssemblyLoadInfo[])null!));
        Assert.Throws<ArgumentException>(() => builder.WithExporter(" "));
        Assert.Throws<ArgumentNullException>(() => builder.WithExporter((string)null!));
        Assert.Throws<ArgumentNullException>(() => builder.WithExporter((Type)null!));
        Assert.Throws<ArgumentNullException>(() => builder.WithScenario((ScenarioBuilder)null!));
        Assert.Throws<ArgumentNullException>(() => builder.WithTimeout((TimeoutBuilder)null!));
        Assert.Throws<ArgumentNullException>(() => builder.WithPathValidations((string[])null!));
    }

    [Fact]
    public void Builder_rejects_null_extension_collections_at_the_single_boundary()
    {
        var config = new Config { Exporters = null! };
        var builder = new ConfigBuilder(config);

        var exception = Assert.Throws<ArgumentException>(() => builder.WithJsonExporterPath("result.json"));
        Assert.Contains("exporters cannot be null", exception.Message);
        Assert.Throws<ArgumentException>(() => builder.Build());
    }

    [Fact]
    public void Builder_identity_preserves_options_and_deduplicates_aliases()
    {
        using var firstDocument = JsonDocument.Parse("1");
        using var secondDocument = JsonDocument.Parse("2");
        var config = new Config();
        var builder = new ConfigBuilder(config)
            .WithExporter("JSON")
            .WithExporter("JsonExporter")
            .WithExporter(new AssemblyLoadInfo
            {
                Name = "custom",
                Options = new Dictionary<string, JsonElement?> { ["value"] = firstDocument.RootElement.Clone() },
            })
            .WithExporter(new AssemblyLoadInfo
            {
                Name = "custom",
                Options = new Dictionary<string, JsonElement?> { ["value"] = secondDocument.RootElement.Clone() },
            });

        Assert.Equal(3, builder.Build().Exporters.Count);
    }

    [Fact]
    public void Builder_deduplicates_built_in_name_and_matching_file_declarations()
    {
        var assemblyPath = typeof(JsonExporter).Assembly.Location;
        var config = new Config();
        config.Exporters.Add(new AssemblyLoadInfo { Name = "Json" });

        var built = new ConfigBuilder(config)
            .WithExporter(new AssemblyLoadInfo
            {
                FilePath = assemblyPath,
                Type = typeof(JsonExporter).FullName,
            })
            .Build();

        Assert.Single(built.Exporters);
    }

    [Fact]
    public void Datadog_alias_enables_datadog_even_when_it_preexists()
    {
        var config = new Config();
        config.Exporters.Add(new AssemblyLoadInfo { Name = "DatadogExporter" });

        var built = new ConfigBuilder(config).WithExporter<DatadogExporter>().Build();

        Assert.True(built.EnableDatadog);
        Assert.Single(built.Exporters);
    }

    [Fact]
    public void Config_file_directory_is_absolute_for_relative_extension_resolution()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"timeit-config-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, "config.json");
        File.WriteAllText(filePath, "{ \"count\": 1, \"enableMetrics\": false, \"processName\": \"echo\", \"scenarios\": [{ \"name\": \"scenario\" }] }");

        try
        {
            var config = Config.LoadConfiguration(filePath);
            Assert.Equal(Path.GetFullPath(config.Path), Path.GetFullPath(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

}

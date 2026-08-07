using System.Text.Json;
using TimeItSharp.Common.Assertors;
using TimeItSharp.Common.Configuration;
using TimeItSharp.Common.Configuration.Builder;
using TimeItSharp.Common.Exporters;
using TimeItSharp.Common.Results;
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
    public void Resolver_rejects_null_entries_with_indices()
    {
        var nullException = Assert.Throws<InvalidOperationException>(() =>
            ExtensionResolver.Resolve<IExporter>(new AssemblyLoadInfo[] { null! }));
        Assert.Contains("index 0", nullException.Message);
    }

    [Fact]
    public void Config_validation_rejects_incomplete_file_selectors()
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
        Assert.Contains(errors, error => error.Contains("assertors[0].filePath is required", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("services[0].type is required", StringComparison.Ordinal));
    }

    [Fact]
    public void Resolver_preserves_redundant_selector_precedence()
    {
        var inMemoryDeclaration = new AssemblyLoadInfo
        {
            Name = "Datadog",
            InMemoryType = typeof(ConstructorNamedExporter),
        };
        var fileDeclaration = new AssemblyLoadInfo
        {
            Name = "Json",
            FilePath = typeof(ConsoleExporter).Assembly.Location,
            Type = typeof(ConsoleExporter).FullName,
        };

        var inMemory = Assert.Single(ExtensionResolver.Resolve<IExporter>(new[] { inMemoryDeclaration }));
        var fromFile = Assert.Single(ExtensionResolver.Resolve<IExporter>(new[] { fileDeclaration }));

        Assert.IsType<ConstructorNamedExporter>(inMemory.Instance);
        Assert.IsType<ConsoleExporter>(fromFile.Instance);
        Assert.Empty(Config.GetAssemblyLoadInfoValidationErrors(inMemoryDeclaration, "exporters[0]"));
        Assert.Empty(Config.GetAssemblyLoadInfoValidationErrors(fileDeclaration, "exporters[0]"));
        Assert.False(ConfigBuilder.Create().WithExporter(inMemoryDeclaration).Build().EnableDatadog);
    }

    [Fact]
    public void Resolver_supports_constructor_initialized_names_without_duplicate_selected_activation()
    {
        ConstructorNamedExporter.ConstructionCount = 0;

        var resolved = Assert.Single(ExtensionResolver.Resolve<IExporter>(new[]
        {
            new AssemblyLoadInfo { Name = ConstructorNamedExporter.ExtensionName },
        }));

        Assert.IsType<ConstructorNamedExporter>(resolved.Instance);
        Assert.Equal(1, ConstructorNamedExporter.ConstructionCount);
    }

    [Fact]
    public void Resolver_type_name_fast_path_does_not_construct_unrelated_extensions()
    {
        UnrelatedExporter.ConstructionCount = 0;

        var resolved = Assert.Single(ExtensionResolver.Resolve<IExporter>(new[]
        {
            new AssemblyLoadInfo { Name = nameof(FastPathExporter) },
        }));

        Assert.IsType<FastPathExporter>(resolved.Instance);
        Assert.Equal(0, UnrelatedExporter.ConstructionCount);
    }

    [Fact]
    public void Resolver_disposes_created_extensions_when_a_later_entry_fails()
    {
        DisposableExporter.Reset();

        Assert.Throws<InvalidOperationException>(() => ExtensionResolver.Resolve<IExporter>(new[]
        {
            new AssemblyLoadInfo { InMemoryType = typeof(DisposableExporter) },
            new AssemblyLoadInfo { InMemoryType = typeof(AbstractExporter) },
        }));

        Assert.Equal(1, DisposableExporter.CreatedCount);
        Assert.Equal(1, DisposableExporter.DisposedCount);
    }

    [Fact]
    public void Resolver_disposes_partial_default_extensions_once_by_identity()
    {
        DisposableExporter.Reset();
        var first = new DisposableExporter();
        var second = new DisposableExporter();

        Assert.Throws<InvalidOperationException>(() => ExtensionResolver.Resolve<IExporter>(
            assemblyLoadInfos: null,
            defaultListFunc: () => new List<IExporter> { first, second, second, null! }));

        Assert.Equal(2, DisposableExporter.CreatedCount);
        Assert.Equal(2, DisposableExporter.DisposedCount);
        Assert.Equal(new[] { 2, 1 }, DisposableExporter.DisposalOrder);
    }

    [Fact]
    public void Resolver_cleanup_failure_does_not_mask_the_primary_failure()
    {
        DisposableExporter.Reset(throwOnDispose: true);
        try
        {
            var exception = Assert.Throws<InvalidOperationException>(() => ExtensionResolver.Resolve<IExporter>(new[]
            {
                new AssemblyLoadInfo { InMemoryType = typeof(DisposableExporter) },
                new AssemblyLoadInfo { InMemoryType = typeof(AbstractExporter) },
            }));

            Assert.Contains("cannot be instantiated", exception.Message);
            Assert.Equal(1, DisposableExporter.DisposedCount);
        }
        finally
        {
            DisposableExporter.Reset();
        }
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

    public sealed class DisposableExporter : IExporter, IDisposable
    {
        private static bool _throwOnDispose;
        private readonly int _id;

        public DisposableExporter() => _id = ++CreatedCount;

        public static int CreatedCount { get; private set; }
        public static int DisposedCount { get; private set; }
        public static List<int> DisposalOrder { get; } = new();
        public string Name => nameof(DisposableExporter);
        public bool Enabled => true;

        public static void Reset(bool throwOnDispose = false)
        {
            CreatedCount = 0;
            DisposedCount = 0;
            DisposalOrder.Clear();
            _throwOnDispose = throwOnDispose;
        }

        public void Initialize(InitOptions options) { }
        public void Export(TimeitResult results) { }

        public void Dispose()
        {
            DisposedCount++;
            DisposalOrder.Add(_id);
            if (_throwOnDispose)
            {
                throw new InvalidOperationException("dispose failure");
            }
        }
    }

    public abstract class AbstractExporter : IExporter
    {
        public string Name => nameof(AbstractExporter);
        public bool Enabled => true;
        public void Initialize(InitOptions options) { }
        public void Export(TimeitResult results) { }
    }

    public sealed class ConstructorNamedExporter : IExporter
    {
        public const string ExtensionName = "constructor-assigned-exporter";
        public static int ConstructionCount;
        private readonly string _name;

        public ConstructorNamedExporter()
        {
            ConstructionCount++;
            _name = ExtensionName;
        }

        public string Name => _name;
        public bool Enabled => true;
        public void Initialize(InitOptions options) { }
        public void Export(TimeItSharp.Common.Results.TimeitResult results) { }
    }

    public sealed class FastPathExporter : IExporter
    {
        public string Name => nameof(FastPathExporter);
        public bool Enabled => true;
        public void Initialize(InitOptions options) { }
        public void Export(TimeItSharp.Common.Results.TimeitResult results) { }
    }

    public sealed class UnrelatedExporter : IExporter
    {
        public static int ConstructionCount;

        public UnrelatedExporter() => ConstructionCount++;

        public string Name => "unrelated-exporter";
        public bool Enabled => true;
        public void Initialize(InitOptions options) { }
        public void Export(TimeItSharp.Common.Results.TimeitResult results) { }
    }

}

using System.Text.Json;
using Spectre.Console;
using TimeItSharp.Common.Results;

namespace TimeItSharp.Common.Exporters;

public sealed class JsonExporter : IExporter
{
    private InitOptions _options;

    public string Name => nameof(JsonExporter);

    public bool Enabled => true;

    public void Initialize(InitOptions options)
    {
        _options = options;
    }

    public void Export(TimeitResult results)
    {
        string? temporaryFile = null;
        IReadOnlyList<string> knownSecrets = Array.Empty<string>();
        try
        {
            // Collect secrets before any path resolution or filesystem work. If a custom result
            // getter fails, the broad environment/template set is still available to sanitize the
            // setup/cleanup exception.
            knownSecrets = Utils.GetSensitiveEnvironmentValues(_options.Configuration?.EnvironmentVariables)
                .Concat(Utils.GetSensitiveEnvironmentSnapshotValues(_options.HostEnvironment))
                .Concat(Utils.GetTemplateSecretValues(_options.TemplateVariables))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (results?.Scenarios is not null)
            {
                knownSecrets = results.Scenarios.Take(Utils.MaxResultCollectionItems)
                    .Where(item => item is not null)
                    .SelectMany(Utils.GetSecretValues)
                    .Concat(knownSecrets)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
            }

            var outputFile = _options.Configuration?.JsonExporterFilePath;
            if (string.IsNullOrWhiteSpace(outputFile))
            {
                outputFile = Path.Combine(Environment.CurrentDirectory, $"jsonexporter_{Random.Shared.Next()}.json");
            }

            // Register the raw caller-selected path before normalization can throw (for example
            // on an invalid path character), because exception messages may echo it verbatim.
            knownSecrets = knownSecrets.Concat(new[] { outputFile })
                .Where(value => !string.IsNullOrEmpty(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var fullOutputFile = Path.GetFullPath(outputFile);
            var outputDirectory = Path.GetDirectoryName(fullOutputFile) ?? Environment.CurrentDirectory;
            // A literal secret can be embedded in a user-selected output path and therefore is
            // absent from environment/template discovery. Treat the resolved path components as
            // redaction candidates before Directory.CreateDirectory or any exception can echo it.
            knownSecrets = knownSecrets.Concat(new[] { outputFile, fullOutputFile, outputDirectory })
                .Where(value => !string.IsNullOrEmpty(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            Directory.CreateDirectory(outputDirectory);

            var safeResult = Utils.SanitizeTimeitResult(results, _options.TemplateVariables, knownSecrets);
            var safeScenarios = safeResult.Scenarios ?? Array.Empty<ScenarioResult>();
            temporaryFile = Path.Combine(
                outputDirectory,
                $".{Path.GetFileName(fullOutputFile)}.{Guid.NewGuid():N}.tmp");

            using (var stream = new FileStream(
                       temporaryFile,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       16 * 1024,
                       FileOptions.SequentialScan))
            {
                JsonSerializer.Serialize(
                    stream,
                    safeScenarios,
                    TimeItResultContext.Default.IReadOnlyListScenarioResult);
                stream.Flush(flushToDisk: true);
            }

            // The temporary file lives beside the destination, so Move is an atomic replacement
            // on the supported local filesystems.  Never expose a half-written JSON document.
            File.Move(temporaryFile, fullOutputFile, overwrite: true);
            temporaryFile = null;
            AnsiConsole.MarkupLine(
                "[lime]The json file '{0}' was exported.[/]",
                Utils.EscapeMarkup(Utils.SanitizeText(fullOutputFile, knownSecrets)));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException)
        {
            AnsiConsole.MarkupLine("[red]Error exporting to json:[/]");
            AnsiConsole.WriteException(Utils.SanitizeException(ex, knownSecrets));
            throw Utils.SanitizeException(ex, knownSecrets);
        }
        finally
        {
            if (temporaryFile is not null)
            {
                try
                {
                    File.Delete(temporaryFile);
                }
                catch (Exception cleanupError) when (cleanupError is not OutOfMemoryException && cleanupError is not StackOverflowException)
                {
                    // Preserve the original export exception.  A best-effort cleanup is safer
                    // than logging a path which could itself contain sensitive metadata.
                }
            }
        }
    }
}

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
            var outputFile = _options.Configuration?.JsonExporterFilePath;
            if (string.IsNullOrWhiteSpace(outputFile))
            {
                outputFile = Path.Combine(Environment.CurrentDirectory, $"jsonexporter_{Random.Shared.Next()}.json");
            }

            var fullOutputFile = Path.GetFullPath(outputFile);
            var outputDirectory = Path.GetDirectoryName(fullOutputFile) ?? Environment.CurrentDirectory;
            Directory.CreateDirectory(outputDirectory);

            // Build a completely separate graph before opening the destination.  A failure while
            // sanitizing or serializing can therefore never truncate a previously valid report.
            knownSecrets = results?.Scenarios is null
                ? Utils.GetSensitiveEnvironmentValues(_options.Configuration?.EnvironmentVariables)
                    .Concat(Utils.GetTemplateSecretValues(_options.TemplateVariables))
                    .ToArray()
                : results.Scenarios.Where(item => item is not null)
                    .SelectMany(Utils.GetSecretValues)
                    .Concat(Utils.GetSensitiveEnvironmentValues(_options.Configuration?.EnvironmentVariables))
                    .Concat(Utils.GetTemplateSecretValues(_options.TemplateVariables))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
            var safeResult = Utils.SanitizeTimeitResult(results, _options.TemplateVariables,
                Utils.GetSensitiveEnvironmentValues(_options.Configuration?.EnvironmentVariables));
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
        catch (Exception ex)
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
                catch
                {
                    // Preserve the original export exception.  A best-effort cleanup is safer
                    // than logging a path which could itself contain sensitive metadata.
                }
            }
        }
    }
}

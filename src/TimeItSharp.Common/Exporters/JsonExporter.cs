using System.Runtime.InteropServices;
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
            var outputFile = _options.Configuration?.JsonExporterFilePath;
            if (string.IsNullOrWhiteSpace(outputFile))
            {
                outputFile = Path.Combine(Environment.CurrentDirectory, $"jsonexporter_{Random.Shared.Next()}.json");
            }

            // Register the raw caller-selected path before normalization can throw (for example
            // on an invalid path character), because exception messages may echo it verbatim.
            knownSecrets = new[] { outputFile }.Concat(knownSecrets)
                .Where(value => !string.IsNullOrEmpty(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var fullOutputFile = Path.GetFullPath(outputFile);
            var outputDirectory = Path.GetDirectoryName(fullOutputFile) ?? Environment.CurrentDirectory;
            var outputBaseName = Path.GetFileName(fullOutputFile);
            temporaryFile = Path.Combine(
                outputDirectory,
                $".{outputBaseName}.{Guid.NewGuid():N}.tmp");
            var temporaryBaseName = Path.GetFileName(temporaryFile);

            // Register every derived path before the first filesystem operation which can echo it.
            knownSecrets = new[]
                {
                    outputFile, fullOutputFile, outputDirectory, outputBaseName,
                    temporaryFile, temporaryBaseName,
                }.Concat(knownSecrets)
                .Where(value => !string.IsNullOrEmpty(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            Directory.CreateDirectory(outputDirectory);

            var safeResult = Utils.SanitizeTimeitResult(results, _options.TemplateVariables, knownSecrets);
            var safeScenarios = safeResult.Scenarios ?? Array.Empty<ScenarioResult>();
            var destinationExists = File.Exists(fullOutputFile);
#if NET7_0_OR_GREATER
            UnixFileMode? destinationMode = null;
            if (destinationExists && !OperatingSystem.IsWindows())
            {
                destinationMode = File.GetUnixFileMode(fullOutputFile);
            }
#endif

            var streamOptions = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                BufferSize = 16 * 1024,
                Options = FileOptions.SequentialScan,
            };
#if NET7_0_OR_GREATER
            if (!OperatingSystem.IsWindows())
            {
                streamOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            }
#endif
            using (var stream = new FileStream(temporaryFile, streamOptions))
            {
                if (!OperatingSystem.IsWindows())
                {
                    // Tighten the newly-created file before any caller-controlled result bytes
                    // are written. NET6 requires chmod because the managed Unix mode APIs were
                    // introduced in NET7.
#if NET7_0_OR_GREATER
                    File.SetUnixFileMode(
                        temporaryFile,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite);
#else
                    if (Chmod(temporaryFile, 0x180) != 0) // 0600
                    {
                        throw new IOException(
                            $"Could not secure temporary export file '{temporaryFile}'.",
                            new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()));
                    }
#endif
                }

                JsonSerializer.Serialize(
                    stream,
                    safeScenarios,
                    TimeItResultContext.Default.IReadOnlyListScenarioResult);
                stream.Flush(flushToDisk: true);
            }

            // Preserve destination security metadata where the platform supports replacement.
            // On Unix the captured mode is applied to the replacement inode above; on Windows,
            // ReplaceFile semantics retain the destination ACL.
            if (destinationExists && OperatingSystem.IsWindows())
            {
                File.Replace(temporaryFile, fullOutputFile, destinationBackupFileName: null);
            }
            else
            {
                File.Move(temporaryFile, fullOutputFile, overwrite: true);
#if NET7_0_OR_GREATER
                if (!OperatingSystem.IsWindows() && destinationMode.HasValue)
                {
                    File.SetUnixFileMode(fullOutputFile, destinationMode.Value);
                }
#endif
            }

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
#if NET6_0
    [DllImport("libc", EntryPoint = "chmod", SetLastError = true)]
    private static extern int Chmod(string path, int mode);
#endif

}

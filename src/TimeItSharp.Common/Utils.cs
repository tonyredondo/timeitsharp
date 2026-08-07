using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MathNet.Numerics.Distributions;
using Spectre.Console;
using TimeItSharp.Common.Results;

namespace TimeItSharp.Common;

internal static class Utils
{
    [ThreadStatic]
    private static StringBuilder? _strBuilder;
    
    /// <summary>
    /// Calculates the standard deviation of a sequence of double-precision floating-point numbers.
    /// </summary>
    /// <param name="data">The sequence of double-precision floating-point numbers.</param>
    /// <returns>The standard deviation, or 0.0 if the calculation results in NaN.</returns>
    public static double StandardDeviation(this IEnumerable<double> data)
    {
        if (data is null)
        {
            return 0d;
        }

        var finiteData = data.Where(double.IsFinite).ToArray();
        if (finiteData.Length < 2)
        {
            return 0d;
        }

        var stdDev = MathNet.Numerics.Statistics.Statistics.StandardDeviation(finiteData);
        return double.IsFinite(stdDev) ? stdDev : 0d;
    }

    /// <summary>
    /// Removes outliers from a sequence of double-precision floating-point numbers.
    /// </summary>
    /// <param name="data">The sequence of double-precision floating-point numbers.</param>
    /// <param name="threshold">The multiplier for the standard deviation. Data points outside of this range will be considered outliers.</param>
    /// <returns>A sequence with the outliers removed.</returns>
    public static IEnumerable<double> RemoveOutliers(IEnumerable<double> data, double threshold)
    {
        if (data is null)
        {
            return [];
        }

        // Non-finite samples cannot participate in a statistical threshold.  Filtering them here
        // keeps every caller (console, JSON and custom assertor) from independently reimplementing
        // the same guard.
        var lstData = data.Where(double.IsFinite).ToList();
        if (lstData.Count == 0)
        {
            return [];
        }

        var stdDev = lstData.StandardDeviation();
        if (!double.IsFinite(stdDev) || stdDev == 0.0 || !double.IsFinite(threshold))
        {
            return lstData;
        }

        var mean = lstData.Average();
        if (!double.IsFinite(mean))
        {
            return lstData;
        }

        return lstData.Where(x =>
        {
            var distance = Math.Abs(x - mean);
            var limit = threshold * stdDev;
            return double.IsFinite(distance) && double.IsFinite(limit) && distance <= limit;
        }).ToList();
    }

    /// <summary>
    /// Converts a time value given in nanoseconds to milliseconds.
    /// </summary>
    /// <param name="nanoseconds">The time in nanoseconds to be converted.</param>
    /// <returns>The time converted to milliseconds.</returns>
    public static double FromNanosecondsToMilliseconds(double nanoseconds)
    {
        if (!double.IsFinite(nanoseconds))
        {
            return 0d;
        }

        // 1 tick is 100 nanoseconds.  Clamp before converting to long: a malformed extension can
        // otherwise overflow a histogram/table render even though the original value was finite.
        var ticksAsDouble = nanoseconds / 100.0;
        if (ticksAsDouble >= TimeSpan.MaxValue.Ticks)
        {
            return TimeSpan.MaxValue.TotalMilliseconds;
        }

        if (ticksAsDouble <= TimeSpan.MinValue.Ticks)
        {
            return TimeSpan.MinValue.TotalMilliseconds;
        }

        return TimeSpan.FromTicks((long)ticksAsDouble).TotalMilliseconds;
    }

    /// <summary>
    /// Converts a TimeSpan object to nanoseconds.
    /// </summary>
    /// <param name="timeSpan">The TimeSpan object to be converted.</param>
    /// <returns>The time in nanoseconds.</returns>
    public static double FromTimeSpanToNanoseconds(TimeSpan timeSpan)
    {
        // 1 tick is 100 nanoseconds
        return (double)timeSpan.Ticks * 100;
    }

    /// <summary>
    /// Determines if a given dataset is bimodal.
    /// </summary>
    /// <param name="data">The dataset to analyze.</param>
    /// <param name="peakCount">Number of peaks detected</param>
    /// <param name="binCount">The number of bins to use for the histogram. Default is 10.</param>
    /// <returns>True if the dataset is bimodal, otherwise false.</returns>
    public static bool IsBimodal(Span<double> data, out int peakCount, int binCount = 10)
    {
        // Return false if there are less than 3 data points, as bimodality can't be determined.
        if (data.Length < 3 || binCount < 3)
        {
            peakCount = 0;
            return false;
        }

        // Initialize variables to find the range of finite data points.
        double min = double.MaxValue, max = double.MinValue;
        var finiteCount = 0;

        // Find the minimum and maximum values in the data while ignoring corrupt samples.
        foreach (var item in data)
        {
            if (!double.IsFinite(item))
            {
                continue;
            }

            finiteCount++;
            if (item < min)
            {
                min = item;
            }

            if (item > max)
            {
                max = item;
            }
        }

        // Constant or insufficient finite samples have no meaningful distribution.
        peakCount = 0;
        if (finiteCount < 3 || max <= min)
        {
            return false;
        }

        // Create and initialize a histogram with 'binCount' bins.
        var histogram = new int[binCount];
        var binWidth = (max - min) / binCount;
        if (!double.IsFinite(binWidth) || binWidth <= 0)
        {
            return false;
        }

        // Populate the histogram based on where each data point falls.
        foreach (var item in data)
        {
            if (!double.IsFinite(item))
            {
                continue;
            }

            var binIndexAsDouble = (item - min) / binWidth;
            if (!double.IsFinite(binIndexAsDouble))
            {
                continue;
            }

            var binIndex = (int)binIndexAsDouble;
            // Handle edge cases caused by rounding at the range boundaries.
            if (binIndex < 0)
            {
                binIndex = 0;
            }
            else if (binIndex >= binCount)
            {
                binIndex = binCount - 1;
            }

            histogram[binIndex]++;
        }

        // Count the peaks in the histogram.
        // A peak is defined as a bin count greater than its neighbors.
        for (var i = 1; i < binCount - 1; i++)
        {
            if (histogram[i] - 1 > histogram[i - 1] && histogram[i] - 1 > histogram[i + 1])
            {
                peakCount++;
            }
        }

        // A dataset is considered bimodal if there are at least two peaks.
        return peakCount >= 2;
    }

    internal const string RedactedValue = "[REDACTED]";

    // Logs are useful when diagnosing a failed benchmark, but an exporter must not turn an
    // accidentally captured process output into an unbounded memory/network sink.  Keep these
    // limits deliberately conservative; they also make the policy deterministic for custom
    // exporters which choose to use the shared sanitizer.
    internal const int MaxExportLogLines = 100;
    internal const int MaxExportLogLineLength = 4096;
    internal const int MaxExportLogCharacters = 64 * 1024;
    internal const int MaxTagEntries = 1024;
    internal const int MaxTagCollectionItems = 512;
    internal const int MaxTagCharacters = 16 * 1024;
    // Result graphs can be supplied by custom assertors/exporters. Keep detached copies bounded
    // even when a hostile extension reports an absurdly large matrix or collection.
    internal const int MaxResultCollectionItems = 100_000;
    internal const int MaxOverheadRows = 1024;
    internal const int MaxOverheadColumns = 1024;
    private const int MaxSanitizationAssignmentSeparators = 128;

    internal sealed class SanitizationBudget
    {
        private int _remainingItems;
        private int _remainingCharacters;

        public SanitizationBudget(int maxItems, int maxCharacters)
        {
            _remainingItems = maxItems;
            _remainingCharacters = maxCharacters;
        }

        public bool TryConsumeItem() => _remainingItems-- > 0;

        public string LimitText(string value)
        {
            if (value.Length <= _remainingCharacters)
            {
                _remainingCharacters -= value.Length;
                return value;
            }

            const string marker = "[TAG TRUNCATED]";
            var length = Math.Max(0, _remainingCharacters - marker.Length);
            _remainingCharacters = 0;
            return value[..length] + marker;
        }
    }

    private sealed class SecretTraversalBudget
    {
        public int Remaining = MaxTagCollectionItems;

        public bool TryConsume() => Remaining-- > 0;
    }

    private static readonly Regex SensitiveAssignment = new(
        "(?<key>[\\\"']?[A-Za-z][A-Za-z0-9_.-]*[\\\"']?)(?<separator>\\s*[:=]\\s*)(?<value>[\\\"'][^\\\"']*[\\\"']|[^,\\s;&}>]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex SensitiveArgument = new(
        "(?<key>(?:--?|/)?[A-Za-z][A-Za-z0-9_.-]*)(?<separator>\\s+|\\s*=\\s*)(?<value>[\\\"'][^\\\"']*[\\\"']|[^\\s,;&>]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Build and MSBuild-style arguments can nest a credential assignment after a switch prefix,
    // e.g. /p:Password=... or -Dpassword=.... The generic assignment expression otherwise sees
    // only the non-sensitive outer `p`/`D` key and consumes the inner assignment as its value.
    private static readonly Regex PrefixedSensitiveAssignment = new(
        @"(?<prefix>(?:--?|/)[A-Za-z0-9_.-]+:?)(?<key>[A-Za-z][A-Za-z0-9_.-]*)(?<separator>\s*[:=]\s*)(?<value>[""'][^""']*[""']|[^,\s;&}>]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex UriUserInfo = new(
        "(?<prefix>://[^/:\\s]+:)(?<value>[^@/\\s]+)(?<suffix>@)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex AuthorizationValue = new(
        "(?<key>\\b(?:authorization|proxy-authorization)\\b)(?<separator>\\s*[:=]\\s*)(?<scheme>bearer|basic)\\s+(?<value>[^\\s,;&}>]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>
    /// Returns whether a metadata key is likely to contain a secret.  Keys are compared after
    /// separators and case have been normalized so aliases such as API-KEY, api_key and ApiKey
    /// share one policy.  Deliberately do not classify PATH as PAT: PAT/JWT/SAS are exact tokens or
    /// suffixes, while the longer aliases use a substring match to cover names such as
    /// GITHUB_TOKEN and AWS_SECRET_ACCESS_KEY.
    /// </summary>
    internal static bool IsSensitiveEnvironmentVariable(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var normalizedName = NormalizeMetadataName(name);
        if (normalizedName.Length == 0)
        {
            return false;
        }

        if (normalizedName is "PAT" or "JWT" or "SAS" or "DDTAGS" ||
            normalizedName.EndsWith("PAT", StringComparison.Ordinal) ||
            normalizedName.EndsWith("JWT", StringComparison.Ordinal) ||
            normalizedName.EndsWith("SAS", StringComparison.Ordinal))
        {
            return true;
        }

        // DATABASE_URL is a common credential-bearing connection endpoint even though the key
        // contains none of the usual TOKEN/PASSWORD words.  Keep the explicit aliases narrow so
        // ordinary URL and PATH metadata remain exportable.
        if (normalizedName.Contains("DATABASEURL", StringComparison.Ordinal) ||
            normalizedName.Equals("DBURL", StringComparison.Ordinal) ||
            normalizedName.Equals("DBURI", StringComparison.Ordinal) ||
            normalizedName.Contains("DATABASEURI", StringComparison.Ordinal) ||
            normalizedName.Contains("REDISURI", StringComparison.Ordinal) ||
            normalizedName.Contains("MONGOURI", StringComparison.Ordinal) ||
            normalizedName.Contains("JDBCURL", StringComparison.Ordinal) ||
            normalizedName.Contains("CONNECTIONURL", StringComparison.Ordinal) ||
            normalizedName.EndsWith("DSN", StringComparison.Ordinal) ||
            normalizedName.Contains("CONNECTIONSTRING", StringComparison.Ordinal) ||
            normalizedName.Contains("ENCRYPTIONKEY", StringComparison.Ordinal) ||
            normalizedName.Contains("SIGNINGKEY", StringComparison.Ordinal) ||
            normalizedName.Contains("GPGKEY", StringComparison.Ordinal) ||
            normalizedName.Contains("SSHKEY", StringComparison.Ordinal) ||
            normalizedName.Contains("WEBHOOK", StringComparison.Ordinal))
        {
            return true;
        }

        return normalizedName.Contains("PASSWORD", StringComparison.Ordinal) ||
               normalizedName.Contains("PASSWD", StringComparison.Ordinal) ||
               normalizedName.Contains("PASS", StringComparison.Ordinal) ||
               normalizedName.Contains("PWD", StringComparison.Ordinal) ||
               normalizedName.Contains("SECRET", StringComparison.Ordinal) ||
               normalizedName.Contains("TOKEN", StringComparison.Ordinal) ||
               normalizedName.Contains("APIKEY", StringComparison.Ordinal) ||
               normalizedName.Contains("APPKEY", StringComparison.Ordinal) ||
               normalizedName.Contains("ACCESSKEY", StringComparison.Ordinal) ||
               normalizedName.Contains("PRIVATEKEY", StringComparison.Ordinal) ||
               normalizedName.Contains("CREDENTIAL", StringComparison.Ordinal) ||
               normalizedName.Contains("AUTH", StringComparison.Ordinal);
    }

    internal static string NormalizeMetadataName(string? name)
    {
        return string.IsNullOrWhiteSpace(name)
            ? string.Empty
            : string.Concat(name.Where(char.IsLetterOrDigit)).ToUpperInvariant();
    }

    /// <summary>
    /// Sanitizes text which can have originated in a process argument, exception, tag, or
    /// standard-output stream.  Name-based assignments are redacted even when the secret was not
    /// present in the environment; known secret values are then replaced everywhere they occur.
    /// </summary>
    internal static string SanitizeText(string? value, IEnumerable<string>? knownSecretValues = null)
    {
        return SanitizeTextCore(value, knownSecretValues, nestedAssignmentDepth: 0);
    }

    private static string SanitizeTextCore(
        string? value,
        IEnumerable<string>? knownSecretValues,
        int nestedAssignmentDepth)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        // Assignment regexes are intentionally conservative, but a target can still emit a very
        // long chain such as A=A=A=.... Reject pathological metadata before regex backtracking or
        // nested assignment passes can consume unbounded CPU.
        if (value.Length > MaxExportLogCharacters ||
            value.Count(character => character is '=' or ':') > MaxSanitizationAssignmentSeparators)
        {
            return RedactedValue;
        }

        // Handle authorization headers before generic assignment redaction so the scheme and the
        // credential token are replaced together.
        var sanitized = AuthorizationValue.Replace(value, match =>
            match.Groups["key"].Value + match.Groups["separator"].Value +
            match.Groups["scheme"].Value + " " + RedactedValue);

        // Nested build arguments can hide more than one assignment (foo=Password=...). Repeat a
        // bounded number of passes so the first outer match cannot suppress the inner credential.
        for (var pass = 0; pass < 4; pass++)
        {
            var previous = sanitized;

            sanitized = SensitiveAssignment.Replace(sanitized, match =>
        {
            var key = TrimMetadataKey(match.Groups["key"].Value);
            if (IsSensitiveEnvironmentVariable(key))
            {
                return match.Groups["key"].Value + match.Groups["separator"].Value + RedactedValue;
            }

            var nestedValue = SanitizeNestedAssignmentValue(match.Groups["value"].Value, knownSecretValues, nestedAssignmentDepth);
            return nestedValue is null
                ? match.Value
                : match.Groups["key"].Value + match.Groups["separator"].Value + nestedValue;
        });

        sanitized = SensitiveArgument.Replace(sanitized, match =>
        {
            var key = TrimMetadataKey(match.Groups["key"].Value);
            if (!IsSensitiveEnvironmentVariable(key))
            {
                var nestedValue = SanitizeNestedAssignmentValue(match.Groups["value"].Value, knownSecretValues, nestedAssignmentDepth);
                return nestedValue is null
                    ? match.Value
                    : match.Groups["key"].Value + match.Groups["separator"].Value + nestedValue;
            }

            var valueGroup = match.Groups["value"].Value;
            var quote = valueGroup.Length > 1 &&
                        ((valueGroup[0] == '"' && valueGroup[^1] == '"') ||
                         (valueGroup[0] == '\'' && valueGroup[^1] == '\''))
                ? valueGroup[0].ToString()
                : string.Empty;
            return match.Groups["key"].Value + match.Groups["separator"].Value +
                   quote + RedactedValue + quote;
        });

        sanitized = PrefixedSensitiveAssignment.Replace(sanitized, match =>
        {
            var key = TrimMetadataKey(match.Groups["key"].Value);
            if (!IsSensitiveEnvironmentVariable(key))
            {
                var nestedValue = SanitizeNestedAssignmentValue(match.Groups["value"].Value, knownSecretValues, nestedAssignmentDepth);
                return nestedValue is null
                    ? match.Value
                    : match.Groups["prefix"].Value + match.Groups["key"].Value +
                      match.Groups["separator"].Value + nestedValue;
            }

            var valueGroup = match.Groups["value"].Value;
            var quote = valueGroup.Length > 1 &&
                        ((valueGroup[0] == '"' && valueGroup[^1] == '"') ||
                         (valueGroup[0] == '\'' && valueGroup[^1] == '\''))
                ? valueGroup[0].ToString()
                : string.Empty;
            return match.Groups["prefix"].Value + match.Groups["key"].Value +
                   match.Groups["separator"].Value + quote + RedactedValue + quote;
        });

            if (string.Equals(previous, sanitized, StringComparison.Ordinal))
            {
                break;
            }
        }

        // Header credentials contain a scheme and token; the generic key/value expression would
        // otherwise redact only the scheme and leave the token after a space.
        sanitized = AuthorizationValue.Replace(sanitized, match =>
            match.Groups["key"].Value + match.Groups["separator"].Value +
            match.Groups["scheme"].Value + " " + RedactedValue);

        // Redact URI user-info passwords.  This is intentionally independent from the key/value
        // regex because a password in a connection URL has no standalone property name.
        sanitized = UriUserInfo.Replace(sanitized, match =>
            match.Groups["prefix"].Value + RedactedValue + match.Groups["suffix"].Value);

        if (knownSecretValues is not null)
        {
            foreach (var secret in knownSecretValues
                         .Take(MaxTagCollectionItems)
                         .Where(item => !string.IsNullOrEmpty(item) && item.Length <= MaxExportLogCharacters)
                         .Distinct(StringComparer.Ordinal)
                         .OrderByDescending(item => item.Length))
            {
                // Even one-character credentials must not be emitted.  Values are collected only
                // from sensitive environment/tag/argument/template sources, so conservative
                // replacement is preferable to allowing a known secret through a text sink.
                if (secret == RedactedValue)
                {
                    continue;
                }

                sanitized = sanitized.Replace(secret, RedactedValue, StringComparison.Ordinal);
            }
        }

        return StripTerminalControls(sanitized);
    }

    private static string StripTerminalControls(string value)
    {
        var builder = new StringBuilder(value.Length);
        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];
            if (current == '\e')
            {
                // Remove CSI and OSC terminal sequences, including OSC hyperlinks/clipboard
                // payloads. An unterminated sequence consumes the remainder of this bounded value.
                if (index + 1 < value.Length && value[index + 1] == '[')
                {
                    index += 2;
                    while (index < value.Length && (value[index] < '@' || value[index] > '~'))
                    {
                        index++;
                    }
                }
                else if (index + 1 < value.Length && value[index + 1] == ']')
                {
                    index += 2;
                    while (index < value.Length)
                    {
                        if (value[index] == '\a')
                        {
                            break;
                        }

                        if (value[index] == '\e' && index + 1 < value.Length && value[index + 1] == '\\')
                        {
                            index++;
                            break;
                        }

                        index++;
                    }
                }

                continue;
            }

            if ((current < ' ' && current is not '\t' and not '\r' and not '\n') ||
                (current >= '\u007f' && current <= '\u009f'))
            {
                continue;
            }

            builder.Append(current);
        }

        return builder.ToString();
    }

    internal static string SanitizeOutput(string? value, IEnumerable<string>? knownSecretValues = null)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaxExportLogCharacters)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : RedactedValue;
        }

        var sanitized = SanitizeText(value, knownSecretValues);
        if (sanitized.Length == 0)
        {
            return string.Empty;
        }

        var lines = sanitized.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
        var builder = new StringBuilder(Math.Min(sanitized.Length, MaxExportLogCharacters));
        var mayTruncate = lines.Length > MaxExportLogLines ||
                          sanitized.Length > MaxExportLogCharacters ||
                          lines.Any(line => line.Length > MaxExportLogLineLength);
        var lineBudget = mayTruncate ? Math.Max(0, MaxExportLogLines - 1) : MaxExportLogLines;
        var lineCount = Math.Min(lines.Length, lineBudget);
        for (var i = 0; i < lineCount; i++)
        {
            var line = lines[i].Length > MaxExportLogLineLength
                ? lines[i][..MaxExportLogLineLength]
                : lines[i];
            if (builder.Length > 0)
            {
                builder.Append('\n');
            }

            builder.Append(line);
            if (builder.Length >= MaxExportLogCharacters)
            {
                builder.Length = MaxExportLogCharacters;
                break;
            }
        }

        var truncated = mayTruncate || lines.Length > lineCount || sanitized.Length > builder.Length;
        if (truncated)
        {
            const string marker = "[OUTPUT TRUNCATED]";
            var separatorLength = builder.Length > 0 ? 1 : 0;
            var requiredLength = separatorLength + marker.Length;
            if (builder.Length + requiredLength > MaxExportLogCharacters)
            {
                builder.Length = Math.Max(0, MaxExportLogCharacters - requiredLength);
            }

            if (builder.Length > 0)
            {
                builder.Append('\n');
            }

            builder.Append(marker);
        }

        return builder.ToString();
    }

    internal static Exception SanitizeException(Exception exception, IEnumerable<string>? knownSecretValues = null)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var message = StripTerminalControls(SanitizeText(exception.Message, knownSecretValues));
        // Do not retain the original exception as InnerException: its ToString() can contain a
        // secret in a nested message or Data value, and Spectre's WriteException traverses it.
        return new Exception($"{exception.GetType().Name}: {message}");
    }

    private static bool LooksLikePath(string value)
    {
        return Path.IsPathRooted(value) ||
               value.Contains('/') ||
               value.Contains('\\') ||
               (value.Length >= 2 && value[1] == ':' &&
                (value[0] is >= 'A' and <= 'Z' or >= 'a' and <= 'z'));
    }

    internal static IReadOnlyList<string> GetPathRedactionValues(IEnumerable<string?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.Take(MaxResultCollectionItems)
            .Where(value => !string.IsNullOrEmpty(value) && value.Length <= MaxExportLogCharacters &&
                            LooksLikePath(value))
            .Select(value => value!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    internal static string EscapeMarkup(string? value)
    {
        return Spectre.Console.Markup.Escape(value ?? string.Empty);
    }

    internal static IReadOnlyList<string> GetSecretValues(ScenarioResult? source)
    {
        var values = new HashSet<string>(StringComparer.Ordinal);
        if (source is null)
        {
            return Array.Empty<string>();
        }

        if (source.EnvironmentVariables is not null)
        {
            foreach (var item in source.EnvironmentVariables.Take(MaxResultCollectionItems))
            {
                if (IsSensitiveEnvironmentVariable(item.Key) && !string.IsNullOrEmpty(item.Value) &&
                    item.Value.Length <= MaxExportLogCharacters)
                {
                    values.Add(item.Value);
                }
            }
        }

        if (source.PathValidations is not null)
        {
            foreach (var path in source.PathValidations.Take(MaxTagEntries)
                         .Where(path => !string.IsNullOrEmpty(path) && path.Length <= MaxExportLogCharacters))
            {
                values.Add(path);
            }
        }

        if (!string.IsNullOrEmpty(source.WorkingDirectory) &&
            source.WorkingDirectory.Length <= MaxExportLogCharacters)
        {
            values.Add(source.WorkingDirectory);
        }

        if (!string.IsNullOrEmpty(source.ProcessName) &&
            source.ProcessName.Length <= MaxExportLogCharacters &&
            LooksLikePath(source.ProcessName))
        {
            values.Add(source.ProcessName);
        }

        if (source.Tags is not null)
        {
            foreach (var item in source.Tags.Take(MaxTagEntries))
            {
                AddSensitiveValues(item.Value, item.Key, values, 0,
                    new HashSet<object>(ReferenceEqualityComparer.Instance), new SecretTraversalBudget());
            }
        }

        AddArgumentSecrets(source.ProcessArguments, values);
        if (source.Timeout is not null)
        {
            AddArgumentSecrets(source.Timeout.ProcessArguments, values);
            AddOpaqueValue(source.Timeout.ProcessName, values);
            AddOpaqueValue(source.Timeout.ProcessArguments, values);
        }

        return values.ToArray();
    }

    internal static IReadOnlyList<string> GetSensitiveEnvironmentValues(
        IReadOnlyDictionary<string, string>? environmentVariables)
    {
        return environmentVariables is null
            ? Array.Empty<string>()
            : environmentVariables.Take(MaxResultCollectionItems)
                .Where(item => IsSensitiveEnvironmentVariable(item.Key) && !string.IsNullOrEmpty(item.Value) &&
                               item.Value.Length <= MaxExportLogCharacters)
                .Select(item => item.Value)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
    }

    internal static IReadOnlyList<string> GetSensitiveEnvironmentSnapshotValues(
        IReadOnlyDictionary<string, string?>? environmentVariables)
    {
        return environmentVariables is null
            ? Array.Empty<string>()
            : environmentVariables.Take(MaxTagCollectionItems)
                .Where(item => IsSensitiveEnvironmentVariable(item.Key) &&
                               !string.IsNullOrEmpty(item.Value) &&
                               item.Value.Length <= MaxExportLogCharacters)
                .Select(item => item.Value!)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
    }

    internal static IReadOnlyList<string> GetTemplateSecretValues(TemplateVariables? templateVariables)
    {
        return templateVariables?.EntriesForSanitization.Take(MaxResultCollectionItems)
                   // Template values are user-controlled and may contain credentials even when the
                   // variable name is innocuous (including the built-in CWD location).
                   .Where(item => !string.IsNullOrEmpty(item.Value) &&
                                  item.Value.Length <= MaxExportLogCharacters)
                   .Select(item => item.Value)
                   .Distinct(StringComparer.Ordinal)
                   .ToArray()
               ?? Array.Empty<string>();
    }

    internal static ScenarioResult SanitizeScenarioResult(ScenarioResult? source,
        TemplateVariables? templateVariables = null,
        IEnumerable<string>? additionalSecretValues = null)
    {
        if (source is null)
        {
            return new ScenarioResult
            {
                Name = string.Empty,
                Status = TimeItSharp.Common.Results.Status.Failed,
                Error = "Scenario result was null."
            };
        }

        var knownSecretsSet = new HashSet<string>(GetSecretValues(source), StringComparer.Ordinal);
        if (additionalSecretValues is not null)
        {
            foreach (var value in additionalSecretValues.Take(MaxResultCollectionItems))
            {
                if (!string.IsNullOrEmpty(value))
                {
                    knownSecretsSet.Add(value);
                }
            }
        }

        foreach (var value in GetTemplateSecretValues(templateVariables))
        {
            knownSecretsSet.Add(value);
        }

        var knownSecrets = knownSecretsSet.ToArray();
        var environmentVariables = new Dictionary<string, string>(StringComparer.Ordinal);
        if (source.EnvironmentVariables is not null)
        {
            foreach (var item in source.EnvironmentVariables.Take(MaxTagEntries))
            {
                var key = SanitizeText(item.Key, knownSecrets);
                if (string.IsNullOrEmpty(key) || key.Length > MaxTagCharacters)
                {
                    continue;
                }

                var safeValue = IsSensitiveEnvironmentVariable(item.Key)
                    ? RedactedValue
                    : SanitizeText(item.Value, knownSecrets);
                environmentVariables[key] = safeValue.Length > MaxTagCharacters
                    ? RedactedValue
                    : safeValue;
            }
        }

        var safeTags = SanitizeTags(source.Tags, templateVariables, knownSecrets);
        var safeTimeout = source.Timeout is null
            ? new Configuration.Timeout()
            : new Configuration.Timeout(
                source.Timeout.MaxDuration,
                SanitizeText(source.Timeout.ProcessName, knownSecrets),
                SanitizeText(source.Timeout.ProcessArguments, knownSecrets));

        var safeMetricsData = new Dictionary<string, List<double>>(StringComparer.Ordinal);
        if (source.MetricsData is not null)
        {
            foreach (var item in source.MetricsData.Take(MaxResultCollectionItems))
            {
                if (string.IsNullOrWhiteSpace(item.Key) || IsSensitiveEnvironmentVariable(item.Key))
                {
                    continue;
                }

                var safeMetricName = SanitizeText(item.Key, knownSecrets);
                if (safeMetricName.Length > MaxTagCharacters)
                {
                    continue;
                }

                safeMetricsData[safeMetricName] = item.Value is null
                    ? new List<double>()
                    : item.Value.Take(MaxResultCollectionItems).Where(double.IsFinite).ToList();
            }
        }

        var safeData = new List<DataPoint>();
        if (source.Data is not null)
        {
            foreach (var item in source.Data.Take(MaxResultCollectionItems))
            {
                if (item is not null)
                {
                    safeData.Add(SanitizeDataPoint(item, knownSecrets));
                }
            }
        }

        return new ScenarioResult
        {
            // Scenario and ParentService are intentionally not copied.  They are runtime object
            // graphs, can retain secrets, and are ignored by the JSON model in any case.
            Scenario = null,
            ParentService = null,
            Name = SanitizeText(source.Name, knownSecrets),
            IsBaseline = source.IsBaseline,
            ProcessName = SanitizeText(source.ProcessName, knownSecrets),
            ProcessArguments = SanitizeText(source.ProcessArguments, knownSecrets),
            WorkingDirectory = SanitizeText(source.WorkingDirectory, knownSecrets),
            EnvironmentVariables = environmentVariables,
            PathValidations = source.PathValidations is null
                ? new List<string>()
                : source.PathValidations.Take(MaxResultCollectionItems).Where(item => item is not null)
                    .Select(item => SanitizeText(item, knownSecrets)).ToList(),
            Timeout = safeTimeout,
            Tags = safeTags,
            Start = source.Start,
            End = source.End,
            Duration = source.Duration < TimeSpan.Zero ? TimeSpan.Zero : source.Duration,
            Error = SanitizeText(source.Error, knownSecrets),
            WarmUpCount = source.WarmUpCount,
            Count = source.Count,
            Data = safeData,
            Durations = source.Durations is null
                ? new List<double>()
                : source.Durations.Take(MaxResultCollectionItems).Where(double.IsFinite).ToList(),
            Outliers = source.Outliers is null
                ? new List<double>()
                : source.Outliers.Take(MaxResultCollectionItems).Where(double.IsFinite).ToList(),
            Mean = FiniteOrZero(source.Mean),
            Median = FiniteOrZero(source.Median),
            Max = FiniteOrZero(source.Max),
            Min = FiniteOrZero(source.Min),
            Stdev = FiniteOrZero(source.Stdev),
            StdErr = FiniteOrZero(source.StdErr),
            P99 = FiniteOrZero(source.P99),
            P95 = FiniteOrZero(source.P95),
            P90 = FiniteOrZero(source.P90),
            Ci99 = source.Ci99 is null ? Array.Empty<double>() : source.Ci99.Take(MaxResultCollectionItems).Where(double.IsFinite).ToArray(),
            Ci95 = source.Ci95 is null ? Array.Empty<double>() : source.Ci95.Take(MaxResultCollectionItems).Where(double.IsFinite).ToArray(),
            Ci90 = source.Ci90 is null ? Array.Empty<double>() : source.Ci90.Take(MaxResultCollectionItems).Where(double.IsFinite).ToArray(),
            IsBimodal = source.IsBimodal,
            PeakCount = source.PeakCount,
            Metrics = CopyFiniteMetrics(source.Metrics, knownSecrets),
            MetricsData = safeMetricsData,
            AdditionalMetrics = CopyFiniteMetrics(source.AdditionalMetrics, knownSecrets),
            Status = source.Status,
            OutliersThreshold = FiniteOrZero(source.OutliersThreshold),
            LastStandardOutput = SanitizeOutput(source.LastStandardOutput, knownSecrets),
        };
    }

    internal static TimeitResult SanitizeTimeitResult(TimeitResult? source,
        TemplateVariables? templateVariables = null,
        IEnumerable<string>? additionalSecretValues = null)
    {
        var scenarios = new List<ScenarioResult>();
        if (source?.Scenarios is not null)
        {
            foreach (var item in source.Scenarios.Take(MaxResultCollectionItems))
            {
                if (item is null)
                {
                    continue;
                }

                var fallbackSecrets = new HashSet<string>(StringComparer.Ordinal);
                try
                {
                    foreach (var secret in GetSecretValues(item))
                    {
                        if (!string.IsNullOrEmpty(secret))
                        {
                            fallbackSecrets.Add(secret);
                        }
                    }

                    if (additionalSecretValues is not null)
                    {
                        foreach (var secret in additionalSecretValues)
                        {
                            if (!string.IsNullOrEmpty(secret))
                            {
                                fallbackSecrets.Add(secret);
                            }
                        }
                    }

                    foreach (var secret in GetTemplateSecretValues(templateVariables))
                    {
                        fallbackSecrets.Add(secret);
                    }

                    scenarios.Add(SanitizeScenarioResult(item, templateVariables, fallbackSecrets));
                }
                catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException)
                {
                    // One malformed custom tag/collection must not suppress all other scenarios.
                    // The fallback contains no source object graph and uses all secrets collected
                    // before the throwing getter so a malformed custom graph cannot bypass policy.
                    string safeName;
                    try
                    {
                        safeName = SanitizeText(item.Name, fallbackSecrets);
                    }
                    catch (Exception nameError) when (nameError is not OutOfMemoryException &&
                                                       nameError is not StackOverflowException)
                    {
                        safeName = string.Empty;
                    }

                    scenarios.Add(new ScenarioResult
                    {
                        Name = safeName,
                        Status = TimeItSharp.Common.Results.Status.Failed,
                        Error = SanitizeException(ex, fallbackSecrets).Message,
                    });
                }
            }
        }

        OverheadResult[][]? overheads = null;
        if (source?.Overheads is not null)
        {
            var rowCount = Math.Min(source.Overheads.Length, MaxOverheadRows);
            overheads = new OverheadResult[rowCount][];
            for (var i = 0; i < rowCount; i++)
            {
                var row = source.Overheads[i];
                if (row is null)
                {
                    overheads[i] = Array.Empty<OverheadResult>();
                    continue;
                }

                overheads[i] = row.Take(MaxOverheadColumns).Select(item => new OverheadResult(
                    FiniteOrZero(item.OverheadPercentage),
                    FiniteOrZero(item.DeltaValue))).ToArray();
            }
        }

        return new TimeitResult { Scenarios = scenarios, Overheads = overheads };
    }

    [UnconditionalSuppressMessage("Trimming", "IL2075",
        Justification = "Extension-defined tag objects are inspected best-effort; missing trimmed properties are safely omitted.")]
    internal static object? SanitizeValue(object? value, string? key = null,
        IEnumerable<string>? knownSecretValues = null, int depth = 0,
        HashSet<object>? visited = null, SanitizationBudget? budget = null)
    {
        if (IsSensitiveEnvironmentVariable(key))
        {
            return RedactedValue;
        }

        if (value is null)
        {
            return null;
        }

        if (depth > 32)
        {
            return RedactedValue;
        }

        budget ??= new SanitizationBudget(MaxTagCollectionItems, MaxTagCharacters);

        if (value is double doubleValue)
        {
            return double.IsFinite(doubleValue) ? doubleValue : null;
        }

        if (value is float floatValue)
        {
            return float.IsFinite(floatValue) ? floatValue : null;
        }

        if (value is string stringValue)
        {
            var sanitizedString = SanitizeText(stringValue, knownSecretValues);
            return budget is null ? sanitizedString : budget.LimitText(sanitizedString);
        }

        if (value is JsonElement jsonElement)
        {
            try
            {
                return SanitizeJsonElement(jsonElement, knownSecretValues, depth, visited, budget);
            }
            catch (Exception jsonError) when (jsonError is not OutOfMemoryException &&
                                                 jsonError is not StackOverflowException)
            {
                // A JsonElement can outlive its JsonDocument.  Treat an unreadable value as
                // sensitive rather than aborting an otherwise valid export.
                return RedactedValue;
            }
        }

        visited ??= new HashSet<object>(ReferenceEqualityComparer.Instance);
        if (!value.GetType().IsValueType && !visited.Add(value))
        {
            return RedactedValue;
        }

        if (value is IDictionary dictionary)
        {
            var result = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (DictionaryEntry item in dictionary)
            {
                var originalKey = Convert.ToString(item.Key, CultureInfo.InvariantCulture) ?? string.Empty;
                var itemKey = SanitizeText(originalKey, knownSecretValues);
                if (budget is not null)
                {
                    itemKey = budget.LimitText(itemKey);
                }

                if (itemKey.Length == 0)
                {
                    continue;
                }

                if (budget is not null && !budget.TryConsumeItem())
                {
                    break;
                }

                var itemValue = SanitizeValue(item.Value, originalKey, knownSecretValues, depth + 1, visited, budget);
                if (itemValue is null && IsNonFiniteNumber(item.Value))
                {
                    continue;
                }

                result[itemKey] = itemValue;
            }

            return result;
        }

        if (value is IEnumerable enumerable && value is not string)
        {
            var result = new List<object?>();
            foreach (var item in enumerable)
            {
                if (budget is not null && !budget.TryConsumeItem())
                {
                    break;
                }

                result.Add(SanitizeValue(item, null, knownSecretValues, depth + 1, visited, budget));
            }

            return result;
        }

        if (value is DateTime or DateTimeOffset or TimeSpan or Guid or decimal or
            int or uint or long or ulong or short or ushort or byte or sbyte or bool)
        {
            return value;
        }

        // JsonElement/dictionaries/arrays are handled above.  For arbitrary user objects, inspect
        // public scalar properties without invoking a serializer that requires runtime code
        // generation (the common library is trim/AOT friendly).  A property getter can throw, so
        // skip that property rather than failing the complete export.  If the object has no useful
        // properties, retain only a sanitized string representation.
        var properties = value.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
        var objectProperties = properties.Where(property =>
            property.CanRead && property.GetIndexParameters().Length == 0).ToArray();
        if (objectProperties.Length > 0)
        {
            var result = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var property in objectProperties)
            {
                try
                {
                    if (budget is not null && !budget.TryConsumeItem())
                    {
                        break;
                    }

                    var propertyName = budget is null
                        ? property.Name
                        : budget.LimitText(property.Name);
                    result[propertyName] = SanitizeValue(
                        property.GetValue(value), propertyName, knownSecretValues, depth + 1, visited, budget);
                }
                catch (Exception propertyError) when (propertyError is not OutOfMemoryException &&
                                                      propertyError is not StackOverflowException)
                {
                    // An extension object is untrusted input.  Do not expose getter exception
                    // text, and do not abort unrelated scenarios.
                }
            }

            return result;
        }

        var scalarText = SanitizeText(Convert.ToString(value, CultureInfo.InvariantCulture), knownSecretValues);
        return budget is null ? scalarText : budget.LimitText(scalarText);
    }

    internal static object? ToDatadogTagValue(object? value, string? key,
        IEnumerable<string>? knownSecretValues = null)
    {
        var safe = SanitizeValue(
            value,
            key,
            knownSecretValues,
            budget: new SanitizationBudget(MaxTagCollectionItems, MaxTagCharacters));
        if (safe is null)
        {
            return null;
        }

        return safe switch
        {
            string or bool or int or uint or long or ulong or short or ushort or byte or sbyte or
                float or double or decimal => safe,
            _ => SerializeSanitizedValue(safe, knownSecretValues)
        };
    }


    private static string SerializeSanitizedValue(object value, IEnumerable<string>? knownSecretValues)
    {
        var builder = new StringBuilder();
        AppendJsonValue(builder, value, knownSecretValues, 0);
        return builder.ToString();
    }

    private static void AppendJsonValue(StringBuilder builder, object? value,
        IEnumerable<string>? knownSecretValues, int depth)
    {
        if (value is null || depth > 32)
        {
            builder.Append("null");
            return;
        }

        switch (value)
        {
            case string stringValue:
                builder.Append('"').Append(JsonEncodedText.Encode(
                    SanitizeText(stringValue, knownSecretValues)).ToString()).Append('"');
                return;
            case bool boolValue:
                builder.Append(boolValue ? "true" : "false");
                return;
            case double doubleValue when double.IsFinite(doubleValue):
                builder.Append(doubleValue.ToString("R", CultureInfo.InvariantCulture));
                return;
            case float floatValue when float.IsFinite(floatValue):
                builder.Append(floatValue.ToString("R", CultureInfo.InvariantCulture));
                return;
            case decimal decimalValue:
                builder.Append(decimalValue.ToString(CultureInfo.InvariantCulture));
                return;
            case IFormattable formattable:
                builder.Append('"').Append(JsonEncodedText.Encode(
                    SanitizeText(formattable.ToString(null, CultureInfo.InvariantCulture), knownSecretValues)).ToString()).Append('"');
                return;
            case IDictionary dictionary:
                builder.Append('{');
                var firstProperty = true;
                foreach (DictionaryEntry item in dictionary)
                {
                    var key = SanitizeText(
                        Convert.ToString(item.Key, CultureInfo.InvariantCulture), knownSecretValues);
                    if (key.Length == 0)
                    {
                        continue;
                    }

                    if (!firstProperty)
                    {
                        builder.Append(',');
                    }

                    firstProperty = false;
                    builder.Append('"').Append(JsonEncodedText.Encode(key).ToString()).Append("\":");
                    AppendJsonValue(builder, item.Value, knownSecretValues, depth + 1);
                }

                builder.Append('}');
                return;
            case IEnumerable enumerable when value is not string:
                builder.Append('[');
                var firstItem = true;
                foreach (var item in enumerable)
                {
                    if (!firstItem)
                    {
                        builder.Append(',');
                    }

                    firstItem = false;
                    AppendJsonValue(builder, item, knownSecretValues, depth + 1);
                }

                builder.Append(']');
                return;
            default:
                builder.Append('"').Append(JsonEncodedText.Encode(
                    SanitizeText(Convert.ToString(value, CultureInfo.InvariantCulture), knownSecretValues)).ToString()).Append('"');
                return;
        }
    }

    private static string? SanitizeNestedAssignmentValue(
        string value,
        IEnumerable<string>? knownSecretValues,
        int nestedAssignmentDepth)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        // Never recursively parse attacker-controlled assignment chains without a hard bound.
        // Very large nested values are replaced before the outer sink can materialize them.
        if (nestedAssignmentDepth >= 4 || value.Length > MaxExportLogCharacters)
        {
            return RedactedValue;
        }

        var quote = value.Length > 1 &&
                    ((value[0] == '"' && value[^1] == '"') ||
                     (value[0] == '\'' && value[^1] == '\''))
            ? value[0].ToString()
            : string.Empty;
        var unquoted = quote.Length == 0 ? value : value[1..^1];
        var sanitized = SanitizeTextCore(unquoted, knownSecretValues, nestedAssignmentDepth + 1);
        if (sanitized == unquoted)
        {
            return null;
        }

        return quote + sanitized + quote;
    }

    private static string TrimMetadataKey(string key)
    {
        return key.Trim().Trim('"', '\'').TrimStart('-', '/');
    }

    private static bool IsNonFiniteNumber(object? value)
    {
        return value is double d && !double.IsFinite(d) ||
               value is float f && !float.IsFinite(f);
    }

    private static void AddOpaqueValue(string? value, ISet<string> values)
    {
        if (!string.IsNullOrEmpty(value) && value.Length <= MaxExportLogCharacters)
        {
            values.Add(value);
        }
    }

    private static void AddArgumentSecrets(string? arguments, ISet<string> values)
    {
        if (string.IsNullOrEmpty(arguments))
        {
            return;
        }

        if (arguments.Length > MaxExportLogCharacters ||
            arguments.Count(character => character is '=' or ':') > MaxSanitizationAssignmentSeparators)
        {
            return;
        }

        foreach (Match match in SensitiveAssignment.Matches(arguments))
        {
            if (IsSensitiveEnvironmentVariable(TrimMetadataKey(match.Groups["key"].Value)))
            {
                AddUnquotedValue(match.Groups["value"].Value, values);
            }
        }

        foreach (Match match in SensitiveArgument.Matches(arguments))
        {
            if (IsSensitiveEnvironmentVariable(TrimMetadataKey(match.Groups["key"].Value)))
            {
                AddUnquotedValue(match.Groups["value"].Value, values);
            }
        }
    }

    private static void AddUnquotedValue(string value, ISet<string> values)
    {
        var trimmed = value.Trim();
        if (trimmed.Length > 1 &&
            ((trimmed[0] == '"' && trimmed[^1] == '"') ||
             (trimmed[0] == '\'' && trimmed[^1] == '\'')))
        {
            trimmed = trimmed[1..^1];
        }

        if (!string.IsNullOrEmpty(trimmed))
        {
            values.Add(trimmed);
        }
    }

    [UnconditionalSuppressMessage("Trimming", "IL2075",
        Justification = "Secret discovery over extension-defined objects is best-effort and getters are guarded.")]
    private static void AddSensitiveValues(object? value, string? key, ISet<string> values,
        int depth, HashSet<object> visited, SecretTraversalBudget budget)
    {
        if (value is null || depth > 32 || !budget.TryConsume())
        {
            return;
        }

        if (IsSensitiveEnvironmentVariable(key))
        {
            AddStringValues(value, values, depth, visited, budget);
            return;
        }

        if (value is JsonElement jsonElement)
        {
            try
            {
                switch (jsonElement.ValueKind)
                {
                    case JsonValueKind.Object:
                        foreach (var item in jsonElement.EnumerateObject())
                        {
                            AddSensitiveValues(item.Value, item.Name, values, depth + 1, visited, budget);
                        }
                        break;
                    case JsonValueKind.Array:
                        foreach (var item in jsonElement.EnumerateArray())
                        {
                            AddSensitiveValues(item, null, values, depth + 1, visited, budget);
                        }
                        break;
                }
            }
            catch (Exception jsonError) when (jsonError is not OutOfMemoryException &&
                                                 jsonError is not StackOverflowException)
            {
                // Unreadable DOM values are redacted by the sanitizer itself.
            }

            return;
        }

        if (!value.GetType().IsValueType && !visited.Add(value))
        {
            return;
        }

        if (value is IDictionary dictionary)
        {
            foreach (DictionaryEntry item in dictionary)
            {
                AddSensitiveValues(item.Value,
                    Convert.ToString(item.Key, CultureInfo.InvariantCulture), values, depth + 1, visited, budget);
            }
        }
        else if (value is IEnumerable enumerable && value is not string)
        {
            foreach (var item in enumerable)
            {
                AddSensitiveValues(item, null, values, depth + 1, visited, budget);
            }
        }
        else
        {
            foreach (var property in value.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                         .Where(item => item.CanRead && item.GetIndexParameters().Length == 0))
            {
                try
                {
                    AddSensitiveValues(property.GetValue(value), property.Name, values, depth + 1, visited, budget);
                }
                catch (Exception propertyError) when (propertyError is not OutOfMemoryException &&
                                                      propertyError is not StackOverflowException)
                {
                    // Ignore untrusted getter failures.
                }
            }
        }
    }

    [UnconditionalSuppressMessage("Trimming", "IL2075",
        Justification = "Secret discovery over extension-defined objects is best-effort and getters are guarded.")]
    private static void AddStringValues(object? value, ISet<string> values, int depth,
        HashSet<object> visited, SecretTraversalBudget budget)
    {
        if (value is null || depth > 32 || !budget.TryConsume())
        {
            return;
        }

        if (value is string stringValue)
        {
            if (!string.IsNullOrEmpty(stringValue))
            {
                values.Add(stringValue);
            }

            return;
        }

        if (value is JsonElement jsonElement)
        {
            try
            {
                switch (jsonElement.ValueKind)
                {
                    case JsonValueKind.String:
                        AddStringValues(jsonElement.GetString(), values, depth + 1, visited, budget);
                        break;
                    case JsonValueKind.Object:
                        foreach (var item in jsonElement.EnumerateObject())
                        {
                            AddStringValues(item.Value, values, depth + 1, visited, budget);
                        }
                        break;
                    case JsonValueKind.Array:
                        foreach (var item in jsonElement.EnumerateArray())
                        {
                            AddStringValues(item, values, depth + 1, visited, budget);
                        }
                        break;
                }
            }
            catch (Exception jsonError) when (jsonError is not OutOfMemoryException &&
                                                 jsonError is not StackOverflowException)
            {
                // Disposed/malformed DOM values are handled as redacted by SanitizeValue.
            }

            return;
        }

        if (!value.GetType().IsValueType && !visited.Add(value))
        {
            return;
        }

        if (value is IDictionary dictionary)
        {
            foreach (DictionaryEntry item in dictionary)
            {
                AddStringValues(item.Value, values, depth + 1, visited, budget);
            }
        }
        else if (value is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                AddStringValues(item, values, depth + 1, visited, budget);
            }
        }
        else
        {
            // A custom tag object may hide a secret in a property rather than a dictionary.  Only
            // collect values from properties whose names identify credentials; this avoids turning
            // every ordinary string in metadata into a global replacement candidate.
            foreach (var property in value.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                         .Where(item => item.CanRead && item.GetIndexParameters().Length == 0 &&
                                        IsSensitiveEnvironmentVariable(item.Name)))
            {
                try
                {
                    AddStringValues(property.GetValue(value), values, depth + 1, visited, budget);
                }
                catch (Exception propertyError) when (propertyError is not OutOfMemoryException &&
                                                      propertyError is not StackOverflowException)
                {
                    // Ignore untrusted getter failures.
                }
            }
        }
    }

    private static object? SanitizeJsonElement(JsonElement element, IEnumerable<string>? knownSecretValues,
        int depth, HashSet<object>? visited, SanitizationBudget? budget)
    {
        if (depth > 32)
        {
            return RedactedValue;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
            {
                var result = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var item in element.EnumerateObject())
                {
                    if (budget is not null && !budget.TryConsumeItem())
                    {
                        break;
                    }

                    var propertyName = SanitizeText(item.Name, knownSecretValues);
                    if (budget is not null)
                    {
                        propertyName = budget.LimitText(propertyName);
                    }

                    if (propertyName.Length == 0)
                    {
                        continue;
                    }

                    result[propertyName] = IsSensitiveEnvironmentVariable(item.Name)
                        ? RedactedValue
                        : SanitizeJsonElement(item.Value, knownSecretValues, depth + 1, visited, budget);
                }

                return result;
            }
            case JsonValueKind.Array:
            {
                var result = new List<object?>();
                foreach (var item in element.EnumerateArray())
                {
                    if (budget is not null && !budget.TryConsumeItem())
                    {
                        break;
                    }

                    result.Add(SanitizeJsonElement(item, knownSecretValues, depth + 1, visited, budget));
                }

                return result;
            }
            case JsonValueKind.String:
            {
                var sanitizedString = SanitizeText(element.GetString(), knownSecretValues);
                return budget is null ? sanitizedString : budget.LimitText(sanitizedString);
            }
            case JsonValueKind.Number:
                return element.TryGetDecimal(out var decimalValue)
                    ? decimalValue
                    : element.TryGetDouble(out var doubleValue) && double.IsFinite(doubleValue)
                        ? doubleValue
                        : null;
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            default:
                return null;
        }
    }

    private static Dictionary<string, object> SanitizeTags(
        IReadOnlyDictionary<string, object>? source,
        TemplateVariables? templateVariables,
        IReadOnlyList<string> knownSecrets)
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        if (source is null)
        {
            return result;
        }

        foreach (var item in source.Take(MaxTagEntries))
        {
            var key = item.Key ?? string.Empty;
            key = templateVariables is null ? key : templateVariables.Expand(key);
            key = SanitizeText(key, knownSecrets);
            if (key.Length > 1024)
            {
                key = key[..1024];
            }

            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var budget = new SanitizationBudget(MaxTagCollectionItems, MaxTagCharacters);
            var value = SanitizeValue(item.Value, key, knownSecrets, budget: budget);
            if (value is null && IsNonFiniteNumber(item.Value))
            {
                continue;
            }

            if (value is string stringValue && templateVariables is not null)
            {
                value = budget.LimitText(SanitizeText(templateVariables.Expand(stringValue), knownSecrets));
            }

            result[key] = value!;
        }

        return result;
    }

    private static DataPoint SanitizeDataPoint(DataPoint source, IReadOnlyList<string> knownSecrets)
    {
        var metrics = CopyFiniteMetrics(source.Metrics, knownSecrets);
        var response = source.AssertResults;
        var message = SanitizeText(response.Message, knownSecrets);
        return new DataPoint
        {
            Start = source.Start,
            End = source.End,
            Duration = source.Duration < TimeSpan.Zero ? TimeSpan.Zero : source.Duration,
            Metrics = metrics,
            StandardOutput = SanitizeOutput(source.StandardOutput, knownSecrets),
            Scenario = null,
            AssertResults = new Assertors.AssertResponse(response.Status, response.ShouldContinue, message),
        };
    }

    private static Dictionary<string, double> CopyFiniteMetrics(
        IReadOnlyDictionary<string, double>? source, IReadOnlyList<string> knownSecrets)
    {
        var result = new Dictionary<string, double>(StringComparer.Ordinal);
        if (source is null)
        {
            return result;
        }

        foreach (var item in source.Take(MaxResultCollectionItems))
        {
            if (!double.IsFinite(item.Value) || string.IsNullOrWhiteSpace(item.Key) ||
                IsSensitiveEnvironmentVariable(item.Key))
            {
                continue;
            }

            var key = SanitizeText(item.Key, knownSecrets);
            if (!string.IsNullOrWhiteSpace(key) && key.Length <= MaxTagCharacters)
            {
                result[key] = item.Value;
            }
        }

        return result;
    }

    private static double FiniteOrZero(double value)
    {
        return double.IsFinite(value) ? value : 0d;
    }

    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        internal static readonly ReferenceEqualityComparer Instance = new();

        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }

    /// <summary>
    /// Calculates the interquartile range (IQR) of a sorted dataset.
    /// </summary>
    /// <param name="sortedData">The sorted dataset.</param>
    /// <returns>The IQR of the dataset.</returns>
    public static double CalculateIQR(double[] sortedData)
    {
        if (sortedData is null || sortedData.Length == 0)
        {
            return 0d;
        }

        var n = sortedData.Length;
        if (n < 2)
        {
            return 0;
        }

        if (n == 2)
        {
            return sortedData[1] - sortedData[0];
        }

        double Q1, Q3;

        if (n % 2 == 0)
        {
            Q1 = (sortedData[n / 4] + sortedData[n / 4 - 1]) / 2.0;
            Q3 = (sortedData[3 * n / 4] + sortedData[3 * n / 4 - 1]) / 2.0;
        }
        else
        {
            Q1 = sortedData[n / 4];
            Q3 = sortedData[3 * n / 4];
        }

        return Q3 - Q1;
    }

    /// <summary>
    /// Generates a comparison table based on a list of ScenarioResult objects.
    /// Each cell [i, j] in the table contains the overhead percentage and delta value
    /// of the mean of results[j] over results[i].
    /// </summary>
    /// <param name="results">A read-only list of ScenarioResult objects.</param>
    /// <returns>A 2D array containing the comparison data, or an empty array if the input list is null or empty.</returns>
    public static OverheadResult[][] GetComparisonTableData(IReadOnlyList<ScenarioResult> results)
    {
        // Check if the results list is null or empty
        if (results is null || results.Count == 0)
        {
            return [];
        }

        // Keep direct callers from forcing a quadratic allocation before an exporter can apply
        // its detached-graph limits.
        var rowCount = Math.Min(results.Count, MaxOverheadRows);
        var columnCount = Math.Min(results.Count, MaxOverheadColumns);
        var tableData = new OverheadResult[rowCount][];

        // Loop through each pair of results to populate the table
        for (var i = 0; i < rowCount; i++)
        {
            tableData[i] = new OverheadResult[columnCount];
            for (var j = 0; j < columnCount; j++)
            {
                // Retrieve the mean values for the i-th and j-th results
                var firstItem = results[i];
                var firstMean = firstItem?.Mean ?? 0d;

                var secondItem = results[j];
                var secondMean = secondItem?.Mean ?? 0d;

                // A zero baseline has no meaningful relative overhead.
                var overheadPercentage = firstMean == 0
                    ? 0
                    : ((secondMean * 100) / firstMean) - 100;
                overheadPercentage = double.IsFinite(overheadPercentage)
                    ? Math.Round(overheadPercentage, 1)
                    : 0;

                // Calculate the delta value
                var deltaValue = secondMean - firstMean;
                deltaValue = double.IsFinite(deltaValue) ? Math.Round(deltaValue, 1) : 0;

                // Store the results in the table using the constructor
                tableData[i][j] = new OverheadResult(overheadPercentage, deltaValue);
            }
        }

        return tableData;
    }

    /// <summary>
    /// Retrieves the width of the console buffer safely. 
    /// If unable to determine the width, returns a default value.
    /// </summary>
    /// <param name="defaultValue">The default value to return if the width cannot be determined. Default is 180.</param>
    /// <returns>The width of the console buffer, or the default value if it cannot be determined.</returns>
    public static int GetSafeWidth(int defaultValue = 280)
    {
        try
        {
            // Attempt to get the console buffer width.
            var width = System.Console.BufferWidth;

            // If the buffer width is reported as zero, use the default value.
            if (width == 0)
            {
                width = defaultValue;
            }

            return width;
        }
        catch (Exception ex) when (ex is IOException or ArgumentOutOfRangeException or IndexOutOfRangeException)
        {
            // Console hosts and redirected test runners can report an invalid buffer width.
            return defaultValue;
        }
    }
    
    /// <summary>
    /// Converts a TimeSpan object to a human-readable string.
    /// </summary>
    /// <param name="timeSpan">Timespan</param>
    /// <returns>human-readable string</returns>
    public static string ToDurationString(this TimeSpan timeSpan)
    {
        _strBuilder ??= new StringBuilder();
        if (timeSpan.Hours > 0)
        {
            _strBuilder.Append($"{timeSpan.Hours} h ");
        }

        if (timeSpan.Minutes > 0)
        {
            _strBuilder.Append($"{timeSpan.Minutes} min ");
        }
        
        if (timeSpan.Seconds > 0)
        {
            _strBuilder.Append($"{timeSpan.Seconds} sec ");
        }
        
        _strBuilder.Append($"{timeSpan.Milliseconds} ms");
        var value = _strBuilder.ToString();
        _strBuilder.Clear();
        return value;
    }
    
    public static double[] CalculateConfidenceInterval(double mean, double standardError, int sampleSize, double confidenceLevel)
    {
        var safeMean = double.IsFinite(mean) ? mean : 0d;
        var safeError = double.IsFinite(standardError) && standardError >= 0 ? standardError : 0d;
        if (sampleSize <= 1 || !double.IsFinite(confidenceLevel) || confidenceLevel <= 0 || confidenceLevel >= 1)
        {
            return [safeMean, safeMean];
        }

        try
        {
            var degreesOfFreedom = sampleSize - 1;
            var criticalValue = StudentT.InvCDF(0, 1, degreesOfFreedom,
                1 - (1 - confidenceLevel) / 2);
            var marginOfError = criticalValue * safeError;
            if (!double.IsFinite(marginOfError))
            {
                return [safeMean, safeMean];
            }

            var lowerBound = safeMean - marginOfError;
            var upperBound = safeMean + marginOfError;
            if (!double.IsFinite(lowerBound) || !double.IsFinite(upperBound))
            {
                return [safeMean, safeMean];
            }

            return [lowerBound, upperBound];
        }
        catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException)
        {
            AnsiConsole.WriteException(SanitizeException(ex));
            return [safeMean, safeMean];
        }
    }
}

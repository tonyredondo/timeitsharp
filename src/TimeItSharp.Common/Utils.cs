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
    internal const int MaxResultGraphCharacters = 4 * 1024 * 1024;
    internal const int MaxOverheadRows = 1024;
    internal const int MaxOverheadColumns = 1024;
    internal const int MaxConsoleScenarios = 64;
    internal const int MaxConsoleMetrics = 256;
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

        public bool TryConsumeItem()
        {
            if (_remainingItems <= 0)
            {
                return false;
            }

            _remainingItems--;
            return true;
        }

        public string LimitText(string value)
        {
            if (_remainingCharacters <= 0)
            {
                return string.Empty;
            }

            if (value.Length <= _remainingCharacters)
            {
                _remainingCharacters -= value.Length;
                return value;
            }

            const string marker = "[TAG TRUNCATED]";
            var remaining = _remainingCharacters;
            var markerLength = Math.Min(marker.Length, remaining);
            var valueLength = Math.Max(0, remaining - markerLength);
            _remainingCharacters = 0;
            return value[..valueLength] + marker[..markerLength];
        }
    }

    private sealed class SecretTraversalBudget
    {
        public int Remaining = MaxResultCollectionItems;

        public bool TryConsume()
        {
            if (Remaining <= 0)
            {
                return false;
            }

            Remaining--;
            return true;
        }
    }

    private sealed class ResultGraphBudget
    {
        private int _remainingItems = MaxResultCollectionItems;
        private int _remainingCharacters = MaxResultGraphCharacters;

        public bool TryConsumeItem()
        {
            if (_remainingItems <= 0)
            {
                return false;
            }

            _remainingItems--;
            return true;
        }

        public string LimitText(string? value)
        {
            if (string.IsNullOrEmpty(value) || _remainingCharacters <= 0)
            {
                return string.Empty;
            }

            if (value.Length <= _remainingCharacters)
            {
                _remainingCharacters -= value.Length;
                return value;
            }

            const string marker = "[RESULT TRUNCATED]";
            var remaining = _remainingCharacters;
            var markerLength = Math.Min(marker.Length, remaining);
            var valueLength = Math.Max(0, remaining - markerLength);
            _remainingCharacters = 0;
            return value[..valueLength] + marker[..markerLength];
        }
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
        "(?<key>\\b(?:authorization|proxy-authorization)\\b)(?<separator>\\s*[:=]\\s*)(?<value>[^\\r\\n]*)",
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

        // PWD and OLDPWD are standard shell working-directory variables, not password aliases.
        // Keep compound names such as DB_PWD sensitive through the substring rule below.
        if (normalizedName is "PWD" or "OLDPWD")
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

        // Normalize terminal controls before recognizing security-sensitive structure. Otherwise
        // CR or bidi/format controls can split a key such as Author\rization and bypass the
        // Authorization/assignment expressions, while disappearing only at the terminal sink.
        var safeInput = StripTerminalControls(value);
        if (safeInput.Length == 0)
        {
            return string.Empty;
        }

        IReadOnlyList<string>? knownSecrets = null;
        if (knownSecretValues is not null &&
            !TrySnapshotKnownSecrets(knownSecretValues, out knownSecrets))
        {
            // An incomplete secret set is unsafe: a value beyond the cap or after a throwing
            // enumerator could otherwise be emitted verbatim.
            return RedactedValue;
        }

        // Assignment regexes are intentionally conservative, but a target can still emit a very
        // long chain such as A=A=A=.... Reject pathological metadata before regex backtracking or
        // nested assignment passes can consume unbounded CPU.
        if (safeInput.Length > MaxExportLogCharacters ||
            safeInput.Count(character => character is '=' or ':') > MaxSanitizationAssignmentSeparators)
        {
            return RedactedValue;
        }

        // Handle authorization headers before generic assignment redaction so the complete field
        // value is removed independently of its authentication scheme.
        var sanitized = AuthorizationValue.Replace(safeInput, match =>
            match.Groups["key"].Value + match.Groups["separator"].Value + RedactedValue);

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

            var nestedValue = SanitizeNestedAssignmentValue(match.Groups["value"].Value, knownSecrets, nestedAssignmentDepth);
            return nestedValue is null
                ? match.Value
                : match.Groups["key"].Value + match.Groups["separator"].Value + nestedValue;
        });

        sanitized = SensitiveArgument.Replace(sanitized, match =>
        {
            var key = TrimMetadataKey(match.Groups["key"].Value);
            if (!IsSensitiveEnvironmentVariable(key))
            {
                var nestedValue = SanitizeNestedAssignmentValue(match.Groups["value"].Value, knownSecrets, nestedAssignmentDepth);
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
                var nestedValue = SanitizeNestedAssignmentValue(match.Groups["value"].Value, knownSecrets, nestedAssignmentDepth);
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
            match.Groups["key"].Value + match.Groups["separator"].Value + RedactedValue);

        // Redact URI user-info passwords.  This is intentionally independent from the key/value
        // regex because a password in a connection URL has no standalone property name.
        sanitized = UriUserInfo.Replace(sanitized, match =>
            match.Groups["prefix"].Value + RedactedValue + match.Groups["suffix"].Value);

        if (knownSecrets is not null)
        {
            foreach (var secret in knownSecrets
                         .Where(item => !string.IsNullOrEmpty(item))
                         .Select(StripTerminalControls)
                         .Where(item => item.Length > 0 && item.Length <= MaxExportLogCharacters)
                         .Distinct(StringComparer.Ordinal)
                         .OrderByDescending(item => item.Length))
            {
                // Secret replacement must never turn a bounded source string into a much larger
                // result (for example, replacing a one-character secret thousands of times).
                if (secret == RedactedValue)
                {
                    continue;
                }

                sanitized = ReplaceSecretBounded(
                    sanitized,
                    secret,
                    Math.Min(MaxExportLogCharacters, Math.Max(safeInput.Length, RedactedValue.Length)));
                if (sanitized == RedactedValue)
                {
                    break;
                }
            }
        }

        if (sanitized.Length > MaxExportLogCharacters)
        {
            return RedactedValue;
        }

        return StripTerminalControls(sanitized);
    }

    private static bool TrySnapshotKnownSecrets(
        IEnumerable<string> source, out IReadOnlyList<string>? snapshot)
    {
        snapshot = null;
        int? reportedCount;
        try
        {
            reportedCount = source switch
            {
                ICollection<string> collection => collection.Count,
                IReadOnlyCollection<string> readOnlyCollection => readOnlyCollection.Count,
                ICollection collection => collection.Count,
                _ => null,
            };
        }
        catch (Exception countError) when (countError is not OutOfMemoryException &&
                                           countError is not StackOverflowException)
        {
            return false;
        }

        if (reportedCount is < 0 or > MaxResultCollectionItems)
        {
            return false;
        }

        IEnumerator<string> enumerator;
        try
        {
            enumerator = source.GetEnumerator();
        }
        catch (Exception enumerationError) when (enumerationError is not OutOfMemoryException &&
                                                  enumerationError is not StackOverflowException)
        {
            return false;
        }

        var values = new List<string>(reportedCount ?? 0);
        var succeeded = true;
        try
        {
            while (true)
            {
                bool moved;
                try
                {
                    moved = enumerator.MoveNext();
                }
                catch (Exception enumerationError) when (enumerationError is not OutOfMemoryException &&
                                                          enumerationError is not StackOverflowException)
                {
                    succeeded = false;
                    break;
                }

                if (!moved)
                {
                    break;
                }

                // Probe one item beyond the cap so unknown-count iterators cannot be silently
                // truncated at exactly MaxResultCollectionItems.
                if (values.Count >= MaxResultCollectionItems)
                {
                    succeeded = false;
                    break;
                }

                try
                {
                    values.Add(enumerator.Current);
                }
                catch (Exception enumerationError) when (enumerationError is not OutOfMemoryException &&
                                                          enumerationError is not StackOverflowException)
                {
                    succeeded = false;
                    break;
                }
            }
        }
        finally
        {
            try
            {
                enumerator.Dispose();
            }
            catch (Exception disposeError) when (disposeError is not OutOfMemoryException &&
                                                  disposeError is not StackOverflowException)
            {
                succeeded = false;
            }
        }

        if (!succeeded ||
            reportedCount.HasValue && reportedCount.Value != values.Count)
        {
            return false;
        }

        snapshot = values;
        return true;
    }

    private static string ReplaceSecretBounded(string value, string secret, int maximumLength)
    {
        var first = value.IndexOf(secret, StringComparison.Ordinal);
        if (first < 0)
        {
            return value;
        }

        if (secret.Length < RedactedValue.Length)
        {
            var occurrences = 0;
            for (var index = first; index >= 0;)
            {
                occurrences++;
                index = value.IndexOf(secret, index + secret.Length, StringComparison.Ordinal);
            }

            var resultingLength = (long)value.Length +
                                  (long)occurrences * (RedactedValue.Length - secret.Length);
            if (resultingLength > maximumLength)
            {
                return RedactedValue;
            }
        }

        return value.Replace(secret, RedactedValue, StringComparison.Ordinal);
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

            if (current == '\r' ||
                (current < ' ' && current is not '\t' and not '\n') ||
                (current >= '\u007f' && current <= '\u009f') ||
                char.GetUnicodeCategory(value, index) == UnicodeCategory.Format ||
                IsDefaultIgnorableCodePoint(value, index))
            {
                // CR can rewrite a terminal line, and Unicode format characters include bidi
                // overrides/isolates which can visually disguise the text that follows.
                if (char.IsHighSurrogate(current) && index + 1 < value.Length &&
                    char.IsLowSurrogate(value[index + 1]))
                {
                    index++;
                }

                continue;
            }

            builder.Append(current);
        }

        return builder.ToString();
    }

    private static bool IsDefaultIgnorableCodePoint(string value, int index)
    {
        var codePoint = char.IsHighSurrogate(value[index]) &&
                        index + 1 < value.Length &&
                        char.IsLowSurrogate(value[index + 1])
            ? char.ConvertToUtf32(value[index], value[index + 1])
            : value[index];

        // Unicode Default_Ignorable_Code_Point is broader than General_Category=Format.
        // These ranges cover Other_Default_Ignorable_Code_Point and variation selectors while
        // intentionally retaining ordinary combining marks such as U+0301.
        return codePoint == 0x034F ||
               codePoint is >= 0x115F and <= 0x1160 ||
               codePoint is >= 0x17B4 and <= 0x17B5 ||
               codePoint is >= 0x180B and <= 0x180D ||
               codePoint == 0x180F ||
               codePoint == 0x2065 ||
               codePoint == 0x3164 ||
               codePoint is >= 0xFE00 and <= 0xFE0F ||
               codePoint == 0xFFA0 ||
               codePoint is >= 0xFFF0 and <= 0xFFF8 ||
               codePoint is >= 0x1BCA0 and <= 0x1BCA3 ||
               codePoint is >= 0x1D173 and <= 0x1D17A ||
               codePoint is >= 0xE0000 and <= 0xE0FFF;
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
        return EnumerateSafely(values, MaxResultCollectionItems)
            // These values come from structural path fields.  They must be treated as paths even
            // when relative, extensionless, or otherwise not recognizable by a heuristic.
            .Where(value => !string.IsNullOrEmpty(value) && value.Length <= MaxExportLogCharacters)
            .Select(value => value!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    internal static string EscapeMarkup(string? value)
    {
        return Spectre.Console.Markup.Escape(value ?? string.Empty);
    }

    internal static IReadOnlyList<string> GetSecretValues(
        ScenarioResult? source, IReadOnlyDictionary<string, object>? detachedTags = null)
    {
        var values = new HashSet<string>(StringComparer.Ordinal);
        if (source is null)
        {
            return Array.Empty<string>();
        }

        if (source.EnvironmentVariables is not null)
        {
            foreach (var item in EnumerateSafely(source.EnvironmentVariables, MaxResultCollectionItems))
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
            foreach (var path in EnumerateSafely(source.PathValidations, MaxTagEntries)
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

        var tags = detachedTags ?? source.Tags;
        if (tags is not null)
        {
            foreach (var item in EnumerateSafely(tags, MaxTagEntries))
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
            : EnumerateSafely(environmentVariables, MaxResultCollectionItems)
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
            : EnumerateSafely(environmentVariables, MaxResultCollectionItems)
                .Where(item => IsSensitiveEnvironmentVariable(item.Key) &&
                               !string.IsNullOrEmpty(item.Value) &&
                               item.Value.Length <= MaxExportLogCharacters)
                .Select(item => item.Value!)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
    }

    internal static IReadOnlyList<string> GetTemplateSecretValues(TemplateVariables? templateVariables)
    {
        if (templateVariables is null)
        {
            return Array.Empty<string>();
        }

        return EnumerateSafely(templateVariables.EntriesForSanitization, MaxResultCollectionItems)
            // Template values are user-controlled and may contain credentials even when the
            // variable name is innocuous (including the built-in CWD location).
            .Where(item => !string.IsNullOrEmpty(item.Value) &&
                           item.Value.Length <= MaxExportLogCharacters)
            .Select(item => item.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string SanitizeResultText(
        string? value, IEnumerable<string>? knownSecretValues, ResultGraphBudget budget)
    {
        return budget.LimitText(SanitizeText(value, knownSecretValues));
    }

    private static string RedactStructuralValue(string? value, ResultGraphBudget budget)
    {
        return string.IsNullOrEmpty(value) ? string.Empty : budget.LimitText(RedactedValue);
    }

    internal static ScenarioResult SanitizeScenarioResult(ScenarioResult? source,
        TemplateVariables? templateVariables = null,
        IEnumerable<string>? additionalSecretValues = null)
    {
        var graphBudget = new ResultGraphBudget();
        var detachedTags = source is null ? null : DetachTags(source.Tags, graphBudget);
        IReadOnlyList<string>? additionalSecretSnapshot = null;
        if (additionalSecretValues is not null &&
            !TrySnapshotKnownSecrets(additionalSecretValues, out additionalSecretSnapshot))
        {
            return CreateFailClosedScenarioResult();
        }

        return SanitizeScenarioResultCore(
            source, detachedTags, templateVariables, additionalSecretSnapshot, graphBudget);
    }

    private static ScenarioResult CreateFailClosedScenarioResult()
    {
        return new ScenarioResult
        {
            Name = RedactedValue,
            ProcessName = RedactedValue,
            ProcessArguments = RedactedValue,
            WorkingDirectory = RedactedValue,
            Timeout = new Configuration.Timeout(0, RedactedValue, RedactedValue),
            Error = RedactedValue,
            LastStandardOutput = RedactedValue,
            Status = TimeItSharp.Common.Results.Status.Failed,
        };
    }

    private static ScenarioResult SanitizeScenarioResultCore(
        ScenarioResult? source,
        IReadOnlyDictionary<string, object>? detachedTags,
        TemplateVariables? templateVariables,
        IReadOnlyList<string>? additionalSecretValues,
        ResultGraphBudget graphBudget,
        IReadOnlyList<string>? completeSecretSnapshot = null)
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

        IReadOnlyList<string> knownSecrets;
        if (completeSecretSnapshot is not null)
        {
            knownSecrets = completeSecretSnapshot;
        }
        else
        {
            var knownSecretsSet = new HashSet<string>(
                GetSecretValues(source, detachedTags), StringComparer.Ordinal);
            foreach (var value in additionalSecretValues ?? Array.Empty<string>())
            {
                if (!string.IsNullOrEmpty(value))
                {
                    knownSecretsSet.Add(value);
                }
            }

            foreach (var value in GetTemplateSecretValues(templateVariables))
            {
                knownSecretsSet.Add(value);
            }

            knownSecrets = knownSecretsSet.ToArray();
        }
        var environmentVariables = new Dictionary<string, string>(StringComparer.Ordinal);
        if (source.EnvironmentVariables is not null)
        {
            foreach (var item in EnumerateWithinGraph(source.EnvironmentVariables, MaxTagEntries, graphBudget))
            {
                var key = SanitizeResultText(item.Key, knownSecrets, graphBudget);
                if (string.IsNullOrEmpty(key) || key.Length > MaxTagCharacters)
                {
                    continue;
                }

                var safeValue = IsSensitiveEnvironmentVariable(item.Key)
                    ? RedactedValue
                    : SanitizeResultText(item.Value, knownSecrets, graphBudget);
                environmentVariables[key] = safeValue.Length > MaxTagCharacters
                    ? RedactedValue
                    : safeValue;
            }
        }

        var safeTags = SanitizeTags(detachedTags, templateVariables, knownSecrets, graphBudget);
        var safeTimeout = source.Timeout is null
            ? new Configuration.Timeout()
            : new Configuration.Timeout(
                source.Timeout.MaxDuration,
                RedactStructuralValue(source.Timeout.ProcessName, graphBudget),
                SanitizeResultText(source.Timeout.ProcessArguments, knownSecrets, graphBudget));

        var safeMetricsData = new Dictionary<string, List<double>>(StringComparer.Ordinal);
        if (source.MetricsData is not null)
        {
            foreach (var item in EnumerateWithinGraph(source.MetricsData, MaxResultCollectionItems, graphBudget))
            {
                if (string.IsNullOrWhiteSpace(item.Key) || IsSensitiveEnvironmentVariable(item.Key))
                {
                    continue;
                }

                var safeMetricName = SanitizeResultText(item.Key, knownSecrets, graphBudget);
                if (safeMetricName.Length > MaxTagCharacters)
                {
                    continue;
                }

                safeMetricsData[safeMetricName] = item.Value is null
                    ? new List<double>()
                    : EnumerateWithinGraph(item.Value, MaxResultCollectionItems, graphBudget).Where(double.IsFinite).ToList();
            }
        }

        var safeData = new List<DataPoint>();
        if (source.Data is not null)
        {
            foreach (var item in EnumerateWithinGraph(source.Data, MaxResultCollectionItems, graphBudget))
            {
                if (item is not null)
                {
                    safeData.Add(SanitizeDataPoint(item, knownSecrets, graphBudget));
                }
            }
        }

        return new ScenarioResult
        {
            // Scenario and ParentService are intentionally not copied.  They are runtime object
            // graphs, can retain secrets, and are ignored by the JSON model in any case.
            Scenario = null,
            ParentService = null,
            Name = SanitizeResultText(source.Name, knownSecrets, graphBudget),
            IsBaseline = source.IsBaseline,
            ProcessName = LooksLikePath(source.ProcessName ?? string.Empty)
                ? RedactStructuralValue(source.ProcessName, graphBudget)
                : SanitizeResultText(source.ProcessName, knownSecrets, graphBudget),
            ProcessArguments = SanitizeResultText(source.ProcessArguments, knownSecrets, graphBudget),
            WorkingDirectory = RedactStructuralValue(source.WorkingDirectory, graphBudget),
            EnvironmentVariables = environmentVariables,
            PathValidations = source.PathValidations is null
                ? new List<string>()
                : EnumerateWithinGraph(source.PathValidations, MaxResultCollectionItems, graphBudget).Where(item => item is not null)
                    .Select(item => RedactStructuralValue(item, graphBudget)).ToList(),
            Timeout = safeTimeout,
            Tags = safeTags,
            Start = source.Start,
            End = source.End,
            Duration = source.Duration < TimeSpan.Zero ? TimeSpan.Zero : source.Duration,
            Error = SanitizeResultText(source.Error, knownSecrets, graphBudget),
            WarmUpCount = source.WarmUpCount,
            Count = source.Count,
            Data = safeData,
            Durations = source.Durations is null
                ? new List<double>()
                : EnumerateWithinGraph(source.Durations, MaxResultCollectionItems, graphBudget).Where(double.IsFinite).ToList(),
            Outliers = source.Outliers is null
                ? new List<double>()
                : EnumerateWithinGraph(source.Outliers, MaxResultCollectionItems, graphBudget).Where(double.IsFinite).ToList(),
            Mean = FiniteOrZero(source.Mean),
            Median = FiniteOrZero(source.Median),
            Max = FiniteOrZero(source.Max),
            Min = FiniteOrZero(source.Min),
            Stdev = FiniteOrZero(source.Stdev),
            StdErr = FiniteOrZero(source.StdErr),
            P99 = FiniteOrZero(source.P99),
            P95 = FiniteOrZero(source.P95),
            P90 = FiniteOrZero(source.P90),
            Ci99 = source.Ci99 is null
                ? Array.Empty<double>()
                : EnumerateWithinGraph(source.Ci99, MaxResultCollectionItems, graphBudget)
                    .Where(double.IsFinite).ToArray(),
            Ci95 = source.Ci95 is null
                ? Array.Empty<double>()
                : EnumerateWithinGraph(source.Ci95, MaxResultCollectionItems, graphBudget)
                    .Where(double.IsFinite).ToArray(),
            Ci90 = source.Ci90 is null
                ? Array.Empty<double>()
                : EnumerateWithinGraph(source.Ci90, MaxResultCollectionItems, graphBudget)
                    .Where(double.IsFinite).ToArray(),
            IsBimodal = source.IsBimodal,
            PeakCount = source.PeakCount,
            Metrics = CopyFiniteMetrics(source.Metrics, knownSecrets, graphBudget),
            MetricsData = safeMetricsData,
            AdditionalMetrics = CopyFiniteMetrics(source.AdditionalMetrics, knownSecrets, graphBudget),
            Status = source.Status,
            OutliersThreshold = FiniteOrZero(source.OutliersThreshold),
            LastStandardOutput = graphBudget.LimitText(
                SanitizeOutput(source.LastStandardOutput, knownSecrets)),
        };
    }

    internal static TimeitResult SanitizeTimeitResult(TimeitResult? source,
        TemplateVariables? templateVariables = null,
        IEnumerable<string>? additionalSecretValues = null,
        int maximumScenarios = MaxResultCollectionItems,
        int maximumOverheadDimension = MaxOverheadRows,
        Action<ScenarioResult>? scenarioObserver = null)
    {
        var graphBudget = new ResultGraphBudget();
        var capturedScenarios = new List<(ScenarioResult Source, Dictionary<string, object> Tags)>();
        foreach (var item in EnumerateWithinGraph(
                     source?.Scenarios,
                     Math.Clamp(maximumScenarios, 0, MaxResultCollectionItems), graphBudget))
        {
            if (item is not null)
            {
                var detachedTags = DetachTags(item.Tags, graphBudget);
                if (scenarioObserver is not null)
                {
                    try
                    {
                        // The observer runs while this single source item is captured. It may copy
                        // small value metadata, but the detached graph never retains runtime state.
                        scenarioObserver(item);
                    }
                    catch (Exception observerError) when (observerError is not OutOfMemoryException &&
                                                          observerError is not StackOverflowException)
                    {
                        // Observer failures are untrusted and may contain secrets in their text.
                        // Omit the observation without logging or changing the sanitized result.
                    }
                }

                capturedScenarios.Add((item, detachedTags));
            }
        }

        // Materialize caller-provided secrets once. Partial secret discovery is never safe: if
        // Count disagrees or any enumeration operation fails, return only fixed redacted shells
        // for the source items already captured above.
        IReadOnlyList<string>? additionalSecretSnapshot = null;
        if (additionalSecretValues is not null &&
            !TrySnapshotKnownSecrets(additionalSecretValues, out additionalSecretSnapshot))
        {
            return new TimeitResult
            {
                Scenarios = capturedScenarios
                    .Select(_ => CreateFailClosedScenarioResult())
                    .ToArray(),
                Overheads = null,
            };
        }

        var globalSecrets = new HashSet<string>(StringComparer.Ordinal);
        foreach (var secret in (additionalSecretSnapshot ?? Array.Empty<string>())
                     .Concat(GetTemplateSecretValues(templateVariables)))
        {
            if (!string.IsNullOrEmpty(secret) && secret.Length <= MaxExportLogCharacters)
            {
                globalSecrets.Add(secret);
            }
        }

        foreach (var captured in capturedScenarios)
        {
            foreach (var secret in GetSecretValues(captured.Source, captured.Tags))
            {
                if (!string.IsNullOrEmpty(secret) && secret.Length <= MaxExportLogCharacters)
                {
                    globalSecrets.Add(secret);
                }
            }
        }

        var secrets = globalSecrets.ToArray();
        var scenarios = new List<ScenarioResult>(capturedScenarios.Count);
        foreach (var captured in capturedScenarios)
        {
            try
            {
                scenarios.Add(SanitizeScenarioResultCore(
                    captured.Source, captured.Tags, templateVariables, secrets, graphBudget,
                    completeSecretSnapshot: secrets));
            }
            catch (Exception sanitizationError) when (sanitizationError is not OutOfMemoryException &&
                                                       sanitizationError is not StackOverflowException)
            {
                // Never render text from an untrusted getter/enumerator exception. It can contain
                // both a previously discovered secret and a second value which was never yielded.
                var safeName = SanitizeResultText(captured.Source.Name, secrets, graphBudget);
                scenarios.Add(new ScenarioResult
                {
                    Name = safeName,
                    Status = TimeItSharp.Common.Results.Status.Failed,
                    Error = "Scenario result could not be sanitized.",
                });
            }
        }

        OverheadResult[][]? overheads = null;
        if (source?.Overheads is not null)
        {
            var maximumRows = Math.Min(source.Overheads.Length,
                Math.Clamp(maximumOverheadDimension, 0, MaxOverheadRows));
            var rows = new List<OverheadResult[]>(maximumRows);
            for (var i = 0; i < maximumRows && graphBudget.TryConsumeItem(); i++)
            {
                var row = source.Overheads[i];
                if (row is null)
                {
                    rows.Add(Array.Empty<OverheadResult>());
                    continue;
                }

                var safeRow = new List<OverheadResult>(Math.Min(row.Length, MaxOverheadColumns));
                foreach (var item in EnumerateWithinGraph(
                             row, Math.Min(maximumOverheadDimension, MaxOverheadColumns), graphBudget))
                {
                    safeRow.Add(new OverheadResult(
                        FiniteOrZero(item.OverheadPercentage), FiniteOrZero(item.DeltaValue)));
                }

                rows.Add(safeRow.ToArray());
            }

            overheads = rows.ToArray();
        }

        return new TimeitResult { Scenarios = scenarios, Overheads = overheads };
    }


    [UnconditionalSuppressMessage("Trimming", "IL2075",
        Justification = "Extension-defined tag objects are detached best-effort; missing trimmed properties are safely omitted.")]
    private static object? DetachValue(object? value, ResultGraphBudget budget, int depth = 0,
        HashSet<object>? visited = null)
    {
        if (value is null || depth > 32 || !budget.TryConsumeItem())
        {
            return value is null ? null : RedactedValue;
        }

        if (value is string text)
        {
            return budget.LimitText(text);
        }

        if (value is JsonElement element)
        {
            try
            {
                return element.Clone();
            }
            catch (Exception jsonError) when (jsonError is not OutOfMemoryException &&
                                               jsonError is not StackOverflowException)
            {
                return RedactedValue;
            }
        }

        if (value is DateTime or DateTimeOffset or TimeSpan or Guid or decimal or
            double or float or int or uint or long or ulong or short or ushort or byte or sbyte or bool)
        {
            return value;
        }

        visited ??= new HashSet<object>(ReferenceEqualityComparer.Instance);
        if (!value.GetType().IsValueType && !visited.Add(value))
        {
            return RedactedValue;
        }

        if (value is IDictionary dictionary)
        {
            var detached = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var item in EnumerateDictionarySafely(dictionary, MaxResultCollectionItems))
            {
                if (!budget.TryConsumeItem())
                {
                    break;
                }

                string key;
                try
                {
                    key = budget.LimitText(Convert.ToString(item.Key, CultureInfo.InvariantCulture));
                }
                catch (Exception conversionError) when (conversionError is not OutOfMemoryException &&
                                                         conversionError is not StackOverflowException)
                {
                    continue;
                }

                if (key.Length > 0)
                {
                    detached[key] = DetachValue(item.Value, budget, depth + 1, visited);
                }
            }

            return detached;
        }

        if (value is IEnumerable enumerable && value is not string)
        {
            var detached = new List<object?>();
            IEnumerator? enumerator;
            try
            {
                enumerator = enumerable.GetEnumerator();
            }
            catch (Exception enumerationError) when (enumerationError is not OutOfMemoryException &&
                                                      enumerationError is not StackOverflowException)
            {
                return detached;
            }

            try
            {
                while (budget.TryConsumeItem())
                {
                    bool moved;
                    try
                    {
                        moved = enumerator.MoveNext();
                    }
                    catch (Exception enumerationError) when (enumerationError is not OutOfMemoryException &&
                                                              enumerationError is not StackOverflowException)
                    {
                        break;
                    }

                    if (!moved)
                    {
                        break;
                    }

                    object? current;
                    try
                    {
                        current = enumerator.Current;
                    }
                    catch (Exception enumerationError) when (enumerationError is not OutOfMemoryException &&
                                                              enumerationError is not StackOverflowException)
                    {
                        break;
                    }

                    detached.Add(DetachValue(current, budget, depth + 1, visited));
                }
            }
            finally
            {
                if (enumerator is IDisposable disposable)
                {
                    try
                    {
                        disposable.Dispose();
                    }
                    catch (Exception disposeError) when (disposeError is not OutOfMemoryException &&
                                                          disposeError is not StackOverflowException)
                    {
                    }
                }
            }

            return detached;
        }

        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var property in value.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                     .Where(item => item.CanRead && item.GetIndexParameters().Length == 0))
        {
            if (!budget.TryConsumeItem())
            {
                break;
            }

            object? propertyValue;
            try
            {
                // Read exactly once.  Secret discovery and output sanitization both operate on
                // this detached value, so a stateful getter cannot change between check and use.
                propertyValue = property.GetValue(value);
            }
            catch (Exception propertyError) when (propertyError is not OutOfMemoryException &&
                                                   propertyError is not StackOverflowException)
            {
                continue;
            }

            result[budget.LimitText(property.Name)] =
                DetachValue(propertyValue, budget, depth + 1, visited);
        }

        if (result.Count > 0)
        {
            return result;
        }

        try
        {
            return budget.LimitText(Convert.ToString(value, CultureInfo.InvariantCulture));
        }
        catch (Exception conversionError) when (conversionError is not OutOfMemoryException &&
                                                 conversionError is not StackOverflowException)
        {
            return RedactedValue;
        }
    }

    private static Dictionary<string, object> DetachTags(
        IReadOnlyDictionary<string, object>? source, ResultGraphBudget budget)
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var item in EnumerateSafely(source, MaxTagEntries))
        {
            if (!budget.TryConsumeItem())
            {
                break;
            }

            var key = budget.LimitText(item.Key);
            if (key.Length == 0)
            {
                continue;
            }

            result[key] = DetachValue(item.Value, budget) ?? RedactedValue;
        }

        return result;
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
            foreach (var item in EnumerateDictionarySafely(dictionary, MaxResultCollectionItems))
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
            foreach (var item in EnumerateObjectsSafely(enumerable, MaxResultCollectionItems))
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
                foreach (var item in EnumerateDictionarySafely(dictionary, MaxResultCollectionItems))
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
                foreach (var item in EnumerateObjectsSafely(enumerable, MaxResultCollectionItems))
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

    private static IEnumerable<T> EnumerateWithinGraph<T>(
        IEnumerable<T>? source, int maximumItems, ResultGraphBudget budget)
    {
        foreach (var item in EnumerateSafely(source, maximumItems))
        {
            if (!budget.TryConsumeItem())
            {
                yield break;
            }

            yield return item;
        }
    }

    private static IEnumerable<T> EnumerateSafely<T>(IEnumerable<T>? source, int maximumItems)
    {
        if (source is null || maximumItems <= 0)
        {
            yield break;
        }

        IEnumerator<T> enumerator;
        try
        {
            enumerator = source.GetEnumerator();
        }
        catch (Exception enumerationError) when (enumerationError is not OutOfMemoryException &&
                                                  enumerationError is not StackOverflowException)
        {
            yield break;
        }

        try
        {
            for (var index = 0; index < maximumItems; index++)
            {
                bool moved;
                try
                {
                    moved = enumerator.MoveNext();
                }
                catch (Exception enumerationError) when (enumerationError is not OutOfMemoryException &&
                                                          enumerationError is not StackOverflowException)
                {
                    yield break;
                }

                if (!moved)
                {
                    yield break;
                }

                T current;
                try
                {
                    current = enumerator.Current;
                }
                catch (Exception enumerationError) when (enumerationError is not OutOfMemoryException &&
                                                          enumerationError is not StackOverflowException)
                {
                    yield break;
                }

                yield return current;
            }
        }
        finally
        {
            try
            {
                enumerator.Dispose();
            }
            catch (Exception disposeError) when (disposeError is not OutOfMemoryException &&
                                                  disposeError is not StackOverflowException)
            {
                // An untrusted enumerator cannot make an export fail or expose its exception text.
            }
        }
    }

    private static IEnumerable<object?> EnumerateObjectsSafely(
        IEnumerable source, int maximumItems)
    {
        IEnumerator enumerator;
        try
        {
            enumerator = source.GetEnumerator();
        }
        catch (Exception enumerationError) when (enumerationError is not OutOfMemoryException &&
                                                  enumerationError is not StackOverflowException)
        {
            yield break;
        }

        try
        {
            for (var index = 0; index < maximumItems; index++)
            {
                bool moved;
                try
                {
                    moved = enumerator.MoveNext();
                }
                catch (Exception enumerationError) when (enumerationError is not OutOfMemoryException &&
                                                          enumerationError is not StackOverflowException)
                {
                    yield break;
                }

                if (!moved)
                {
                    yield break;
                }

                object? current;
                try
                {
                    current = enumerator.Current;
                }
                catch (Exception enumerationError) when (enumerationError is not OutOfMemoryException &&
                                                          enumerationError is not StackOverflowException)
                {
                    yield break;
                }

                yield return current;
            }
        }
        finally
        {
            if (enumerator is IDisposable disposable)
            {
                try
                {
                    disposable.Dispose();
                }
                catch (Exception disposeError) when (disposeError is not OutOfMemoryException &&
                                                      disposeError is not StackOverflowException)
                {
                }
            }
        }
    }

    private static IEnumerable<DictionaryEntry> EnumerateDictionarySafely(
        IDictionary dictionary, int maximumItems)
    {
        if (maximumItems <= 0)
        {
            yield break;
        }

        IDictionaryEnumerator enumerator;
        try
        {
            enumerator = dictionary.GetEnumerator();
        }
        catch (Exception enumerationError) when (enumerationError is not OutOfMemoryException &&
                                                  enumerationError is not StackOverflowException)
        {
            yield break;
        }

        try
        {
            for (var index = 0; index < maximumItems; index++)
            {
                bool moved;
                try
                {
                    moved = enumerator.MoveNext();
                }
                catch (Exception enumerationError) when (enumerationError is not OutOfMemoryException &&
                                                          enumerationError is not StackOverflowException)
                {
                    yield break;
                }

                if (!moved)
                {
                    yield break;
                }

                DictionaryEntry current;
                try
                {
                    current = enumerator.Entry;
                }
                catch (Exception enumerationError) when (enumerationError is not OutOfMemoryException &&
                                                          enumerationError is not StackOverflowException)
                {
                    yield break;
                }

                yield return current;
            }
        }
        finally
        {
            if (enumerator is IDisposable disposable)
            {
                try
                {
                    disposable.Dispose();
                }
                catch (Exception disposeError) when (disposeError is not OutOfMemoryException &&
                                                      disposeError is not StackOverflowException)
                {
                }
            }
        }
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
            foreach (var item in EnumerateDictionarySafely(dictionary, MaxResultCollectionItems))
            {
                AddSensitiveValues(item.Value,
                    Convert.ToString(item.Key, CultureInfo.InvariantCulture), values, depth + 1, visited, budget);
            }
        }
        else if (value is IEnumerable enumerable && value is not string)
        {
            foreach (var item in EnumerateObjectsSafely(enumerable, MaxResultCollectionItems))
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
            foreach (var item in EnumerateDictionarySafely(dictionary, MaxResultCollectionItems))
            {
                AddStringValues(item.Value, values, depth + 1, visited, budget);
            }
        }
        else if (value is IEnumerable enumerable)
        {
            foreach (var item in EnumerateObjectsSafely(enumerable, MaxResultCollectionItems))
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
        IReadOnlyList<string> knownSecrets,
        ResultGraphBudget graphBudget)
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        if (source is null)
        {
            return result;
        }

        var budget = new SanitizationBudget(MaxTagCollectionItems, MaxTagCharacters);
        foreach (var item in EnumerateSafely(source, MaxTagEntries))
        {
            var key = item.Key ?? string.Empty;
            key = templateVariables is null ? key : templateVariables.Expand(key);
            key = graphBudget.LimitText(SanitizeText(key, knownSecrets));
            if (key.Length > 1024)
            {
                key = key[..1024];
            }

            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var value = SanitizeValue(item.Value, key, knownSecrets, budget: budget);
            if (value is null && IsNonFiniteNumber(item.Value))
            {
                continue;
            }

            if (value is string stringValue && templateVariables is not null)
            {
                value = graphBudget.LimitText(
                    budget.LimitText(SanitizeText(templateVariables.Expand(stringValue), knownSecrets)));
            }

            if (value is IDictionary or IList)
            {
                try
                {
                    using var document = JsonDocument.Parse(
                        SerializeSanitizedValue(value, knownSecrets));
                    value = document.RootElement.Clone();
                }
                catch (Exception serializationError) when (serializationError is not OutOfMemoryException &&
                                                             serializationError is not StackOverflowException)
                {
                    value = RedactedValue;
                }
            }

            result[key] = value!;
        }

        return result;
    }

    private static DataPoint SanitizeDataPoint(
        DataPoint source, IReadOnlyList<string> knownSecrets, ResultGraphBudget graphBudget)
    {
        var metrics = CopyFiniteMetrics(source.Metrics, knownSecrets, graphBudget);
        var response = source.AssertResults;
        var message = SanitizeResultText(response.Message, knownSecrets, graphBudget);
        return new DataPoint
        {
            Start = source.Start,
            End = source.End,
            Duration = source.Duration < TimeSpan.Zero ? TimeSpan.Zero : source.Duration,
            Metrics = metrics,
            StandardOutput = graphBudget.LimitText(
                SanitizeOutput(source.StandardOutput, knownSecrets)),
            Scenario = null,
            AssertResults = new Assertors.AssertResponse(response.Status, response.ShouldContinue, message),
        };
    }

    private static Dictionary<string, double> CopyFiniteMetrics(
        IReadOnlyDictionary<string, double>? source, IReadOnlyList<string> knownSecrets,
        ResultGraphBudget graphBudget)
    {
        var result = new Dictionary<string, double>(StringComparer.Ordinal);
        if (source is null)
        {
            return result;
        }

        foreach (var item in EnumerateWithinGraph(source, MaxResultCollectionItems, graphBudget))
        {
            if (!double.IsFinite(item.Value) || string.IsNullOrWhiteSpace(item.Key) ||
                IsSensitiveEnvironmentVariable(item.Key))
            {
                continue;
            }

            var key = SanitizeResultText(item.Key, knownSecrets, graphBudget);
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

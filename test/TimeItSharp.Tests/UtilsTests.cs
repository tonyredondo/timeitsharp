using TimeItSharp.Common.Results;

namespace TimeItSharp.Tests;

public sealed class UtilsTests
{
    [Fact]
    public void StandardDeviation_returns_zero_for_empty_and_singleton_samples()
    {
        Assert.Equal(0, Array.Empty<double>().StandardDeviation());
        Assert.Equal(0, new[] { 42d }.StandardDeviation());
    }

    [Fact]
    public void RemoveOutliers_handles_empty_and_constant_samples()
    {
        Assert.Empty(Utils.RemoveOutliers(Array.Empty<double>(), 2));

        var constant = new[] { 3d, 3d, 3d };
        Assert.Equal(constant, Utils.RemoveOutliers(constant, 0));
    }

    [Fact]
    public void Sensitive_environment_names_are_detected_for_redaction()
    {
        Assert.True(Utils.IsSensitiveEnvironmentVariable("DD_API_KEY"));
        Assert.True(Utils.IsSensitiveEnvironmentVariable("client_secret"));
        Assert.True(Utils.IsSensitiveEnvironmentVariable("AUTH_TOKEN"));
        Assert.False(Utils.IsSensitiveEnvironmentVariable("PATH"));
    }

    [Fact]
    public void Sensitive_environment_names_cover_common_secret_aliases()
    {
        Assert.True(Utils.IsSensitiveEnvironmentVariable("AWS_ACCESS_KEY_ID"));
        Assert.True(Utils.IsSensitiveEnvironmentVariable("DB_PASSWD"));
        Assert.True(Utils.IsSensitiveEnvironmentVariable("API-KEY"));
        Assert.True(Utils.IsSensitiveEnvironmentVariable("APP_KEY"));
        Assert.True(Utils.IsSensitiveEnvironmentVariable("ENCRYPTION_KEY"));
        Assert.True(Utils.IsSensitiveEnvironmentVariable("SIGNING_KEY"));
        Assert.True(Utils.IsSensitiveEnvironmentVariable("GPG_KEY"));
        Assert.True(Utils.IsSensitiveEnvironmentVariable("SSH_KEY"));
        Assert.True(Utils.IsSensitiveEnvironmentVariable("SLACK_WEBHOOK"));
        Assert.True(Utils.IsSensitiveEnvironmentVariable("DB_URL"));
        Assert.True(Utils.IsSensitiveEnvironmentVariable("DATABASE_URI"));
        Assert.True(Utils.IsSensitiveEnvironmentVariable("REDIS_URI"));
        Assert.True(Utils.IsSensitiveEnvironmentVariable("MONGO_DSN"));
        Assert.True(Utils.IsSensitiveEnvironmentVariable("DB_URI"));
    }

    [Fact]
    public void Sanitizer_redacts_aliases_without_classifying_path_as_pat()
    {
        Assert.True(Utils.IsSensitiveEnvironmentVariable("GITHUB_PAT"));
        Assert.True(Utils.IsSensitiveEnvironmentVariable("JWT"));
        Assert.True(Utils.IsSensitiveEnvironmentVariable("SAS_TOKEN"));
        Assert.True(Utils.IsSensitiveEnvironmentVariable("DATABASE_URL"));
        Assert.True(Utils.IsSensitiveEnvironmentVariable("api-key"));
        Assert.False(Utils.IsSensitiveEnvironmentVariable("PATH"));
        Assert.False(Utils.IsSensitiveEnvironmentVariable("COMPAT_PATH"));

        var sanitized = Utils.SanitizeText(
            "--password direct-secret --token=token-secret DATABASE_URL=postgres://u:p@h/db",
            ["direct-secret", "token-secret"]);
        Assert.DoesNotContain("direct-secret", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("token-secret", sanitized, StringComparison.Ordinal);
        Assert.Contains(Utils.RedactedValue, sanitized, StringComparison.Ordinal);

        var authorization = Utils.SanitizeText("Authorization: Bearer header-secret");
        Assert.DoesNotContain("header-secret", authorization, StringComparison.Ordinal);

        foreach (var argument in new[]
                 {
                     "/p:foo=Password=supersecret",
                     "-Dpassword=supersecret",
                     "ARG0=Password=supersecret"
                 })
        {
            Assert.DoesNotContain("supersecret", Utils.SanitizeText(argument), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Sanitizer_bounds_output_and_redacts_known_values()
    {
        var output = Utils.SanitizeOutput(string.Join("\n", Enumerable.Repeat("secret-value", 500)), ["secret-value"]);

        Assert.DoesNotContain("secret-value", output, StringComparison.Ordinal);
        Assert.True(output.Split('\n').Length <= Utils.MaxExportLogLines + 1);
        Assert.All(output.Split('\n'), line => Assert.True(line.Length <= Utils.MaxExportLogLineLength ||
            line == "[OUTPUT TRUNCATED]"));
    }

    [Fact]
    public void Sanitizer_removes_c1_terminal_controls()
    {
        var sanitized = Utils.SanitizeText("before\u009b31mRED\u009cafter\u0085");

        Assert.Equal("before31mREDafter", sanitized);
        Assert.DoesNotContain("\u009b", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("\u009c", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("\u0085", sanitized, StringComparison.Ordinal);
    }

    [Fact]
    public void CalculateIQR_handles_two_samples()
    {
        Assert.Equal(8, Utils.CalculateIQR([2, 10]));
    }

    [Fact]
    public void IsBimodal_returns_false_for_constant_or_nonfinite_samples()
    {
        var constant = new[] { 1d, 1d, 1d, 1d };
        Assert.False(Utils.IsBimodal(constant.AsSpan(), out var constantPeakCount));
        Assert.Equal(0, constantPeakCount);

        var nonFinite = new[] { 1d, double.NaN, 2d };
        Assert.False(Utils.IsBimodal(nonFinite.AsSpan(), out var nonFinitePeakCount));
        Assert.Equal(0, nonFinitePeakCount);
    }

    [Fact]
    public void ComparisonTable_handles_empty_and_zero_mean_results()
    {
        Assert.Empty(Utils.GetComparisonTableData(Array.Empty<ScenarioResult>()));
        Assert.Empty(Utils.GetComparisonTableData(null!));

        var results = new[]
        {
            new ScenarioResult { Mean = 0 },
            new ScenarioResult { Mean = 0 },
        };
        var table = Utils.GetComparisonTableData(results);

        Assert.Equal(2, table.Length);
        Assert.Equal(2, table[0].Length);
        Assert.Equal(0, table[0][0].OverheadPercentage);
        Assert.Equal(0, table[0][1].OverheadPercentage);
        Assert.Equal(0, table[1][0].DeltaValue);
    }

    [Fact]
    public void ConfidenceInterval_collapses_when_there_is_no_degrees_of_freedom()
    {
        var interval = Utils.CalculateConfidenceInterval(12.5, 4, 1, 0.95);

        Assert.Equal(new[] { 12.5, 12.5 }, interval);
    }
    [Fact]
    public void Sanitizer_bounds_deep_assignment_chains()
    {
        var nested = string.Join("=", Enumerable.Repeat("A", 500)) + "=Password=SECRETNEST";
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var sanitized = Utils.SanitizeOutput(nested);
        stopwatch.Stop();

        Assert.DoesNotContain("SECRETNEST", sanitized, StringComparison.Ordinal);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5),
            $"Sanitization took {stopwatch.Elapsed}.");
    }

    [Fact]
    public void Sanitizer_redacts_nested_prefixed_assignments()
    {
        var sanitized = Utils.SanitizeOutput(
            "/p:Password=supersecret -Dpassword=also-secret /p:foo=Password=third-secret",
            knownSecretValues: null);

        Assert.DoesNotContain("supersecret", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("also-secret", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("third-secret", sanitized, StringComparison.Ordinal);
        Assert.Contains(Utils.RedactedValue, sanitized, StringComparison.Ordinal);
    }

    [Fact]
    public void Sanitizer_redacts_all_authorization_schemes_and_complete_values()
    {
        var text = "Authorization: Digest username=alice, response=secret\n" +
                   "Proxy-Authorization=Custom first second\nordinary=safe";

        var sanitized = Utils.SanitizeText(text);

        Assert.Equal(
            "Authorization: [REDACTED]\nProxy-Authorization=[REDACTED]\nordinary=safe",
            sanitized);
        Assert.DoesNotContain("Digest", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("Custom", sanitized, StringComparison.Ordinal);
    }

    [Fact]
    public void Sanitizer_processes_more_than_512_known_secrets_without_amplification()
    {
        var secrets = Enumerable.Range(0, 513).Select(index => $"secret-{index:D4}").ToArray();
        var sanitized = Utils.SanitizeText($"echo {secrets[^1]}", secrets);
        var amplified = Utils.SanitizeText(new string('x', 10_000), ["x"]);

        Assert.DoesNotContain(secrets[^1], sanitized, StringComparison.Ordinal);
        Assert.Equal(Utils.RedactedValue, amplified);
        Assert.True(amplified.Length <= Utils.MaxExportLogCharacters);
    }

    [Fact]
    public void Sanitizer_removes_cr_bidi_and_supplementary_format_controls()
    {
        var sanitized = Utils.SanitizeText("a\r\u202Eb\U000E0001\n\t");

        Assert.Equal("ab\n\t", sanitized);
    }

    [Fact]
    public void Scenario_sanitizer_redacts_structural_paths_without_heuristics()
    {
        var scenario = new ScenarioResult
        {
            ProcessName = "tool",
            WorkingDirectory = "relative-directory",
            PathValidations = ["relative-file"],
            Timeout = new TimeItSharp.Common.Configuration.Timeout(1, "helper-tool", "safe"),
        };

        var safe = Utils.SanitizeScenarioResult(scenario);

        Assert.Equal(Utils.RedactedValue, safe.WorkingDirectory);
        Assert.Equal(Utils.RedactedValue, Assert.Single(safe.PathValidations));
        Assert.Equal(Utils.RedactedValue, safe.Timeout.ProcessName);
        Assert.Equal("tool", safe.ProcessName);
        Assert.Equal("relative-directory", scenario.WorkingDirectory);
    }

    [Fact]
    public void Result_graph_uses_one_global_item_budget_across_nested_collections()
    {
        var scenario = new ScenarioResult();
        scenario.MetricsData["first"] = Enumerable.Range(0, 60_000).Select(index => (double)index).ToList();
        scenario.MetricsData["second"] = Enumerable.Range(0, 60_000).Select(index => (double)index).ToList();

        var safe = Utils.SanitizeTimeitResult(new TimeitResult { Scenarios = [scenario] });
        var copied = Assert.Single(safe.Scenarios).MetricsData.Values.Sum(values => values.Count);

        Assert.True(copied < Utils.MaxResultCollectionItems);
    }

    [Fact]
    public void Sanitizer_removes_key_splitting_controls_before_structure_redaction()
    {
        var text = "Autho\rrization: Custom header-secret\n" +
                   "pass\u202Eword=assignment-secret";

        var sanitized = Utils.SanitizeText(text);

        Assert.DoesNotContain("header-secret", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("assignment-secret", sanitized, StringComparison.Ordinal);
        Assert.Equal(2, sanitized.Split(Utils.RedactedValue, StringSplitOptions.None).Length - 1);
        Assert.Equal(Utils.RedactedValue, Utils.SanitizeText("abcd", ["ab\rcd"]));
    }

    [Fact]
    public void Sanitizer_fails_closed_when_known_secret_count_exceeds_global_cap()
    {
        var secrets = new string[Utils.MaxResultCollectionItems + 1];
        Array.Fill(secrets, "secret");

        Assert.Equal(Utils.RedactedValue, Utils.SanitizeText("ordinary", secrets));
        Assert.Equal(Utils.RedactedValue,
            Utils.SanitizeText("ordinary", EnumerateTooManySecrets()));
    }

    [Fact]
    public void Sanitizer_fails_closed_when_known_secret_enumerator_throws()
    {
        Assert.Equal(Utils.RedactedValue,
            Utils.SanitizeText("ordinary", EnumerateThenThrow()));
    }

    [Fact]
    public void Sanitizer_fails_closed_when_known_secret_dispose_throws()
    {
        Assert.Equal(Utils.RedactedValue,
            Utils.SanitizeText("ordinary", new DisposeThrowingSecrets()));
    }

    private static IEnumerable<string> EnumerateTooManySecrets()
    {
        for (var index = 0; index <= Utils.MaxResultCollectionItems; index++)
        {
            yield return "secret";
        }
    }

    private static IEnumerable<string> EnumerateThenThrow()
    {
        yield return "secret";
        throw new InvalidOperationException("secret only in iterator exception");
    }

    [Fact]
    public void Sanitizer_removes_default_ignorables_before_sensitive_key_detection()
    {
        var text = "Author\u034Fization: Custom header-secret\n" +
                   "pass\uFE0Fword=assignment-secret\n" +
                   "ordinary=cafe\u0301";

        var sanitized = Utils.SanitizeText(text);

        Assert.DoesNotContain("header-secret", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("assignment-secret", sanitized, StringComparison.Ordinal);
        Assert.Contains("cafe\u0301", sanitized, StringComparison.Ordinal);
    }

    [Fact]
    public void Sanitizer_fails_closed_when_collection_count_disagrees_with_enumeration()
    {
        Assert.Equal(Utils.RedactedValue,
            Utils.SanitizeText("ordinary", new CountMismatchSecrets()));
    }

    [Fact]
    public void Result_sanitizer_fails_closed_when_additional_secret_enumeration_is_partial()
    {
        var source = new ScenarioResult
        {
            Name = "source-name",
            ProcessName = "source-process",
            Error = "source-error",
        };

        var safe = Utils.SanitizeTimeitResult(
            new TimeitResult { Scenarios = [source] },
            additionalSecretValues: EnumerateThenThrow());
        var scenario = Assert.Single(safe.Scenarios);

        Assert.Equal(Utils.RedactedValue, scenario.Name);
        Assert.Equal(Utils.RedactedValue, scenario.ProcessName);
        Assert.Equal(Utils.RedactedValue, scenario.ProcessArguments);
        Assert.Equal(Utils.RedactedValue, scenario.WorkingDirectory);
        Assert.Equal(Utils.RedactedValue, scenario.Error);
        Assert.Equal(Utils.RedactedValue, scenario.LastStandardOutput);
        Assert.Equal(Status.Failed, scenario.Status);
        Assert.Equal("source-name", source.Name);
    }

    private sealed class CountMismatchSecrets : ICollection<string>
    {
        public int Count => 2;

        public bool IsReadOnly => true;

        public IEnumerator<string> GetEnumerator()
        {
            yield return "secret";
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        public bool Contains(string item) => item == "secret";

        public void CopyTo(string[] array, int arrayIndex) => array[arrayIndex] = "secret";

        public void Add(string item) => throw new NotSupportedException();

        public void Clear() => throw new NotSupportedException();

        public bool Remove(string item) => throw new NotSupportedException();
    }


    private sealed class DisposeThrowingSecrets : IEnumerable<string>
    {
        public IEnumerator<string> GetEnumerator() => new Enumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        private sealed class Enumerator : IEnumerator<string>
        {
            private bool _moved;

            public string Current => "secret";

            object System.Collections.IEnumerator.Current => Current;

            public bool MoveNext()
            {
                if (_moved)
                {
                    return false;
                }

                _moved = true;
                return true;
            }

            public void Reset() => throw new NotSupportedException();

            public void Dispose() => throw new InvalidOperationException("secret in dispose error");
        }
    }

}

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
}

namespace TimeItSharp.Common;

/// <summary>
/// Represents the comparison result between two scenario means,
/// including the overhead percentage and the delta value.
/// </summary>
public readonly struct OverheadResult
{
    /// <summary>
    /// The overhead percentage of the mean value of results[j] over results[i].
    /// </summary>
    public double OverheadPercentage { get; }

    /// <summary>
    /// The difference between the mean values of results[j] and results[i].
    /// </summary>
    public double DeltaValue { get; }

    /// <summary>
    /// Initializes a new instance of the OverheadResult struct.
    /// </summary>
    /// <param name="overheadPercentage">The overhead percentage.</param>
    /// <param name="deltaValue">The delta value.</param>
    public OverheadResult(double overheadPercentage, double deltaValue)
    {
        OverheadPercentage = overheadPercentage;
        DeltaValue = deltaValue;
    }
}
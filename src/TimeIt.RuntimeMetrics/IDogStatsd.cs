namespace TimeIt.RuntimeMetrics;

// The following code is based on: https://github.com/DataDog/dd-trace-dotnet/blob/master/tracer/src/Datadog.Trace/RuntimeMetrics

/// <summary>
/// IDogStatsd is an interface over DogStatsdService.
/// </summary>
public interface IDogStatsd : IDisposable
{
    /// <summary>
    /// Adjusts the specified counter by a given delta.
    /// </summary>
    /// <param name="statName">The name of the metric.</param>
    /// <param name="value">A given delta.</param>
    /// <param name="sampleRate">Percentage of metric to be sent.</param>
    /// <param name="tags">Array of tags to be added to the data.</param>
    void Counter(string statName, double value, double sampleRate = 1, string[]? tags = null);

    /// <summary>
    /// Records the latest fixed value for the specified named gauge.
    /// </summary>
    /// <param name="statName">The name of the metric.</param>
    /// <param name="value">The value of the gauge.</param>
    /// <param name="sampleRate">Percentage of metric to be sent.</param>
    /// <param name="tags">Array of tags to be added to the data.</param>
    void Gauge(string statName, double value, double sampleRate = 1, string[]? tags = null);

    /// <summary>
    /// Increments the specified counter.
    /// </summary>
    /// <param name="statName">The name of the metric.</param>
    /// <param name="value">The amount of increment.</param>
    /// <param name="sampleRate">Percentage of metric to be sent.</param>
    /// <param name="tags">Array of tags to be added to the data.</param>
    void Increment(string statName, int value = 1, double sampleRate = 1, string[]? tags = null);

    /// <summary>
    /// Records an execution time in milliseconds.
    /// </summary>
    /// <param name="statName">The name of the metric.</param>
    /// <param name="value">The time in millisecond.</param>
    /// <param name="sampleRate">Percentage of metric to be sent.</param>
    /// <param name="tags">Array of tags to be added to the data.</param>
    void Timer(string statName, double value, double sampleRate = 1, string[]? tags = null);
}
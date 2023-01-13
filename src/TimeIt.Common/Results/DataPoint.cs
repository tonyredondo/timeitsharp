using System.Text.Json.Serialization;

namespace TimeIt.Common.Results;

public class DataPoint
{
    [JsonPropertyName("start")]
    public DateTime Start { get; set; }

    [JsonPropertyName("end")]
    public DateTime End { get; set; }

    [JsonPropertyName("duration")]
    public TimeSpan Duration { get; set; }

    [JsonPropertyName("error")]
    public string Error { get; set; }

    [JsonPropertyName("metrics")]
    public Dictionary<string, double> Metrics { get; set; }
    
    [JsonIgnore]
    public bool ShouldContinue { get; set; }

    public DataPoint()
    {
        Error = string.Empty;
        Metrics = new Dictionary<string, double>();
    }
}
using System.Text.Json.Serialization;
using TimeIt.Common.Assertors;

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
    public string Error
    {
        get => AssertResults.Message;
        set
        {
            if (AssertResults.Message != value)
            {
                AssertResults = new AssertResponse(AssertResults.Status, AssertResults.ShouldContinue, value);
            }
        }
    }

    [JsonPropertyName("metrics")]
    public Dictionary<string, double> Metrics { get; set; }

    [JsonPropertyName("status")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Status Status
    {
        get => AssertResults.Status; 
        set
        {
            if (AssertResults.Status != value)
            {
                AssertResults = new AssertResponse(value, AssertResults.ShouldContinue, AssertResults.Message);
            }
        }
    }

    [JsonIgnore]
    public bool ShouldContinue
    {
        get => AssertResults.ShouldContinue;
        set
        {
            if (AssertResults.ShouldContinue != value)
            {
                AssertResults = new AssertResponse(AssertResults.Status, value, AssertResults.Message);
            }
        }
    }
    
    [JsonIgnore]
    public AssertResponse AssertResults { get; set; }

    public DataPoint()
    {
        Error = string.Empty;
        Metrics = new Dictionary<string, double>();
        AssertResults = new AssertResponse(Status.Passed);
    }
}
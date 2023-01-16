using Newtonsoft.Json;

namespace TimeIt.RuntimeMetrics;

public class FileStatsd : IDogStatsd
{
    private readonly StreamWriter _streamWriter;

    public FileStatsd(string filePath)
    {
        _streamWriter = new StreamWriter(filePath, true);
    }

    public void Counter(string statName, double value, double sampleRate = 1, string[]? tags = null)
    {
        lock (_streamWriter)
        {
            _streamWriter.WriteLine(JsonConvert.SerializeObject(new FileStatsdPayload("counter", statName, value)));
        }
    }

    public void Gauge(string statName, double value, double sampleRate = 1, string[]? tags = null)
    {
        lock (_streamWriter)
        {
            _streamWriter.WriteLine(JsonConvert.SerializeObject(new FileStatsdPayload("gauge", statName, value)));
        }
    }

    public void Increment(string statName, int value = 1, double sampleRate = 1, string[]? tags = null)
    {
        lock (_streamWriter)
        {
            _streamWriter.WriteLine(JsonConvert.SerializeObject(new FileStatsdPayload("increment", statName, value)));
        }
    }

    public void Timer(string statName, double value, double sampleRate = 1, string[]? tags = null)
    {
        lock (_streamWriter)
        {
            _streamWriter.WriteLine(JsonConvert.SerializeObject(new FileStatsdPayload("timer", statName, value)));
        }
    }

    public void Dispose()
    {
        _streamWriter.Dispose();
    }
    
    public class FileStatsdPayload
    {
        public FileStatsdPayload(string type, string name, double value)
        {
            Type = type;
            Name = name;
            Value = value;
        }

        [JsonProperty("type")]
        public string? Type { get; set; }
        [JsonProperty("name")]
        public string? Name { get; set; }
        [JsonProperty("value")]
        public double Value { get; set; }
    }
}

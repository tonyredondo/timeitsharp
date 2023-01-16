using Newtonsoft.Json;

namespace TimeIt.RuntimeMetrics;

public class FileStatsd : IDogStatsd
{
    private readonly string _filePath;
    private readonly string[] _lines;

    public FileStatsd(string filePath)
    {
        _filePath = filePath;
        _lines = new string[1];
    }

    public void Counter(string statName, double value, double sampleRate = 1, string[]? tags = null)
    {
        lock (_filePath)
        {
            _lines[0] = JsonConvert.SerializeObject(new FileStatsdPayload("counter", statName, value, sampleRate, tags));
            File.AppendAllLines(_filePath, _lines);
        }
    }

    public void Gauge(string statName, double value, double sampleRate = 1, string[]? tags = null)
    {
        lock (_filePath)
        {
            _lines[0] = JsonConvert.SerializeObject(new FileStatsdPayload("gauge", statName, value, sampleRate, tags));
            File.AppendAllLines(_filePath, _lines);
        }
    }

    public void Increment(string statName, int value = 1, double sampleRate = 1, string[]? tags = null)
    {
        lock (_filePath)
        {
            _lines[0] = JsonConvert.SerializeObject(new FileStatsdPayload("increment", statName, value, sampleRate, tags));
            File.AppendAllLines(_filePath, _lines);
        }
    }

    public void Timer(string statName, double value, double sampleRate = 1, string[]? tags = null)
    {
        lock (_filePath)
        {
            _lines[0] = JsonConvert.SerializeObject(new FileStatsdPayload("timer", statName, value, sampleRate, tags));
            File.AppendAllLines(_filePath, _lines);
        }
    }

    public void Dispose()
    {
    }
    
    public class FileStatsdPayload
    {
        public FileStatsdPayload()
        {
            Date = DateTime.UtcNow.ToBinary();
        }
        public FileStatsdPayload(string type, string name, object value, double sampleRate, string[]? tags)
        {
            Date = DateTime.UtcNow.ToBinary();
            Type = type;
            Name = name;
            Value = value;
            SampleRate = sampleRate;
            Tags = tags;
        }

        [JsonProperty("date")]
        public long Date { get; set; }
        [JsonProperty("type")]
        public string? Type { get; set; }
        [JsonProperty("name")]
        public string? Name { get; set; }
        [JsonProperty("value")]
        public object? Value { get; set; }
        [JsonProperty("sampleRate")]
        public double? SampleRate { get; set; }
        [JsonProperty("tags")]
        public string[]? Tags { get; set; }
    }
}

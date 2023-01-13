using Newtonsoft.Json;

namespace TimeIt.Configuration
{
    public class ProcessData
	{
		[JsonProperty("processName")]
        public string? ProcessName { get; set; }
		[JsonProperty("processArguments")]
        public string? ProcessArguments { get; set; }
		[JsonProperty("workingDirectory")]
        public string? WorkingDirectory { get; set; }
		[JsonProperty("environmentVariables")]
        public Dictionary<string, string> EnvironmentVariables { get; set; }
		[JsonProperty("timeout")]
        public Timeout? Timeout { get; set; }
		[JsonProperty("tags")]
        public Dictionary<string, string> Tags { get; set; }

		public ProcessData()
		{
			EnvironmentVariables = new Dictionary<string, string>();
			Tags = new Dictionary<string, string>();
		}

        public ProcessData(string? processName, string? processArguments, string? workingDirectory,
			Dictionary<string, string> environmentVariables, Timeout? timeout, Dictionary<string, string> tags)
		{
			ProcessName = processName;
			ProcessArguments = processArguments;
			WorkingDirectory = workingDirectory;
			EnvironmentVariables = environmentVariables;
			Timeout = timeout;
			Tags = tags;
		}
    }
}


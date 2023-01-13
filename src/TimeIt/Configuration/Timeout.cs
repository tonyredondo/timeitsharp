using System;
using Newtonsoft.Json;

namespace TimeIt.Configuration
{
    class Timeout
	{
		[JsonProperty("maxDuration")]
		public int MaxDuration { get; set; }
		[JsonProperty("processName")]
		public string? ProcessName { get; set; }
		[JsonProperty("processArguments")]
        public string? ProcessArguments { get; set; }

		public Timeout()
		{
			MaxDuration = 0;
		}

		public Timeout(int maxDuration, string? processName, string? processArguments)
		{
			MaxDuration = maxDuration;
			ProcessName = processName;
			ProcessArguments = processArguments;
		}
	}
}


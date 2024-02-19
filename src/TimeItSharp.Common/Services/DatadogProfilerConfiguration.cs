using System.Text.Json;

namespace TimeItSharp.Common.Services;

public sealed class DatadogProfilerConfiguration
{
    internal Dictionary<string, bool>? EnabledScenarios { get; private set; }
    internal bool UseExtraRun { get; private set; }
    internal int ExtraRunCount { get; private set; }

    public DatadogProfilerConfiguration(Dictionary<string, JsonElement?>? options = null)
    {
        if (options is not null)
        {
            if (options.TryGetValue("useExtraRun", out var useExtraRunJsonElement) &&
                useExtraRunJsonElement is not null)
            {
                UseExtraRun = useExtraRunJsonElement.Value.GetBoolean();
            }

            if (options.TryGetValue("extraRunCount", out var extraRunCountJsonElement) &&
                extraRunCountJsonElement is not null)
            {
                ExtraRunCount = extraRunCountJsonElement.Value.GetInt32();
            }

            if (options.TryGetValue("scenarios", out var scenariosJsonElement) &&
                scenariosJsonElement is not null)
            {
                EnabledScenarios = new Dictionary<string, bool>();
                foreach (var scenarioItem in scenariosJsonElement.Value.EnumerateArray())
                {
                    EnabledScenarios[scenarioItem.GetString() ?? string.Empty] = true;
                }
            }
        }
    }
    
    public DatadogProfilerConfiguration WithScenario(string scenario, bool enabled)
    {
        EnabledScenarios ??= new();
        EnabledScenarios[scenario] = enabled;
        return this;
    }

    public DatadogProfilerConfiguration WithExtraRun(int count = 0)
    {
        UseExtraRun = true;
        ExtraRunCount = count;
        return this;
    }
}
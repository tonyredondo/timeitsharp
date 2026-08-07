using TimeItSharp.Common.Configuration;

namespace TimeItSharp.Common;

public record struct InitOptions(Config Configuration, AssemblyLoadInfo? LoadInfo, TemplateVariables TemplateVariables, object? State)
{
    /// <summary>
    /// Host environment captured at engine entry. Extensions must use this snapshot instead of
    /// reading process-global environment variables during initialization.
    /// </summary>
    public IReadOnlyDictionary<string, string?>? HostEnvironment { get; init; }
}
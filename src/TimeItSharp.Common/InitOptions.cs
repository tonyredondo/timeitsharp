using TimeItSharp.Common.Configuration;

namespace TimeItSharp.Common;

public record struct InitOptions(Config Configuration, AssemblyLoadInfo? LoadInfo, TemplateVariables TemplateVariables, object? State);
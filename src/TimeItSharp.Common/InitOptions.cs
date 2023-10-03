using TimeItSharp.Common.Configuration;

namespace TimeItSharp.Common;

public record struct InitOptions(Config Configuration, TemplateVariables TemplateVariables, object? State);
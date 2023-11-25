using System.Text.Json.Serialization;

namespace TimeItSharp.Common.Configuration;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Config))]
[JsonSerializable(typeof(IConvertible))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
internal partial class ConfigContext : JsonSerializerContext
{
}
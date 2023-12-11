using System.Text.Json;
using System.Text.Json.Serialization;

namespace TimeItSharp.Common.Results;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(TimeitResult))]
[JsonSerializable(typeof(IConvertible))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(JsonElement))]
internal partial class TimeItResultContext : JsonSerializerContext
{
}
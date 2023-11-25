using System.Text.Json.Serialization;

namespace TimeItSharp.Common;

[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(FileStatsdPayload))]
internal partial class FileStatsdPayloadContext : JsonSerializerContext
{
}
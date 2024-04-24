using System.Text.Json.Serialization;

namespace Publisher;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(UserCreated))]
[JsonSerializable(typeof(UserDeleted))]
internal partial class SourceGenerationContext: JsonSerializerContext
{
}

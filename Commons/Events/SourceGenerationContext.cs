using System.Text.Json.Serialization;

namespace Commons.Events;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(UserCreated))]
[JsonSerializable(typeof(UserDeleted))]
public partial class SourceGenerationContext: JsonSerializerContext
{
}

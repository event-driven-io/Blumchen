using System.Text.Json.Serialization;
using Blumchen.Serialization;

namespace Tests;

[MessageUrn("user-created:v1")]
internal record UserCreated(
    Guid Id,
    string Name
);

[MessageUrn("user-deleted:v1")]
internal record UserDeleted(
    Guid Id,
    string Name
);

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(UserCreated))]
[JsonSerializable(typeof(UserDeleted))]
internal partial class SourceGenerationContext: JsonSerializerContext{}

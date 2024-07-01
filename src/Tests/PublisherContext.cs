using System.Text.Json.Serialization;
using Blumchen.Serialization;

namespace Tests;

[MessageUrn("user-created:v1")]
internal record PublisherUserCreated(
    Guid Id,
    string Name
);

[MessageUrn("user-deleted:v1")]
internal record PublisherUserDeleted(
    Guid Id,
    string Name
);

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(PublisherUserCreated))]
[JsonSerializable(typeof(PublisherUserDeleted))]
internal partial class PublisherContext: JsonSerializerContext;

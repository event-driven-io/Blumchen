using System.Text.Json.Serialization;
using Blumchen.Serialization;

namespace Tests;

[MessageRoutedByUrn("user-created:v1")]
internal record PublisherUserCreated(
    Guid Id,
    string Name
);

[MessageRoutedByUrn("user-deleted:v1")]
internal record PublisherUserDeleted(
    Guid Id,
    string Name
);

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(PublisherUserCreated))]
[JsonSerializable(typeof(PublisherUserDeleted))]
internal partial class PublisherContext: JsonSerializerContext;

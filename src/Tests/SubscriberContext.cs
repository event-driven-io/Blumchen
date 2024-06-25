using System.Text.Json.Serialization;
using Blumchen.Serialization;

namespace Tests;

[MessageUrn("user-created:v1")]
internal record SubscriberUserCreated(
    Guid Id,
    string Name
);

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(SubscriberUserCreated))]
internal partial class SubscriberContext: JsonSerializerContext;

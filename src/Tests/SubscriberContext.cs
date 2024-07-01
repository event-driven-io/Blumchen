using System.Text.Json.Serialization;
using Blumchen.Serialization;
// ReSharper disable All

namespace Tests;

[MessageUrn("user-created:v1")]
internal record SubscriberUserCreated(
     Guid Id,
     string Name
)
{
    //JsonRequired is here a Guard clause to prevent null data
    [JsonRequired] public Guid Id { get; init; } = Id;
    [JsonRequired] public string Name { get; init; } = Name;
};

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(SubscriberUserCreated))]
internal partial class SubscriberContext: JsonSerializerContext;

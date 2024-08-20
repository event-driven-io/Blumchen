using System.Text.Json.Serialization;
using Blumchen.Serialization;

namespace SubscriberWorker
{
    [MessageRoutedByUrn("user-created:v1")]
    public record UserCreatedContract(
        Guid Id,
        string Name
    );

    [MessageRoutedByUrn("user-deleted:v1")]
    public record UserDeletedContract(
        Guid Id,
        string Name
    );

    [MessageRoutedByUrn("user-modified:v1")] //subscription ignored
    public record UserModifiedContract(
        Guid Id,
        string Name = "Modified"
    );

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(UserCreatedContract))]
    [JsonSerializable(typeof(UserDeletedContract))]
    [JsonSerializable(typeof(UserModifiedContract))]
    internal partial class SourceGenerationContext: JsonSerializerContext;
}

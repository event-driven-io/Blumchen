using System.Text.Json.Serialization;
using Blumchen.Serialization;

namespace Subscriber
{
    [MessageUrn("user-created:v1")]
    public record UserCreatedContract(
        Guid Id,
        string Name
    );

    [MessageUrn("user-deleted:v1")]
    public record UserDeletedContract(
        Guid Id,
        string Name
    );

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(UserCreatedContract))]
    [JsonSerializable(typeof(UserDeletedContract))]
    internal partial class SourceGenerationContext: JsonSerializerContext;
}

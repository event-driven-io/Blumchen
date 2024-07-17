using System.Text.Json.Serialization;
using Blumchen.Serialization;

namespace Subscriber
{
    [MessageUrn("user-created:v1")]
    public record UserCreatedContract(
        Guid Id,
        string Name
    );

    [RawUrn("user-deleted:v1", RawData.Object)]
    public interface MessageObjects;


    [RawUrn("user-modified:v1", RawData.String)] 
    internal interface MessageString;

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(UserCreatedContract))]
    internal partial class SourceGenerationContext: JsonSerializerContext;
}

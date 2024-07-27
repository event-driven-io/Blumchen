using System.Text.Json.Serialization;
using Blumchen.Serialization;

namespace UnitTests
{
    [MessageUrn("user-created:v1")]
    public record UserCreatedContract(
        Guid Id,
        string Name
    );

    [RawUrn("user-deleted:v1")]
    public class MessageObjects;


    [RawUrn("user-modified:v1")]
    internal class MessageString;

    public class InvalidMessage;

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(UserCreatedContract))]
    internal partial class SourceGenerationContext: JsonSerializerContext;
}

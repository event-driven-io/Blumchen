using System.Text.Json.Serialization;
using Blumchen.Serialization;

namespace UnitTests
{
    [MessageRoutedByUrn("user-created:v1")]
    public record UserCreatedContract(
        Guid Id,
        string Name
    );

    [MessageRoutedByUrn("user-registered:v1")]
    public record UserRegisteredContract(
        Guid Id,
        string Name
    );

    [RawRoutedByUrn("user-deleted:v1")]
    public class MessageObjects;


    [RawRoutedByUrn("user-modified:v1")]
    internal class MessageString;

    public class InvalidMessage;

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(UserCreatedContract))]
    [JsonSerializable(typeof(UserRegisteredContract))]
    internal partial class SourceGenerationContext: JsonSerializerContext;
}

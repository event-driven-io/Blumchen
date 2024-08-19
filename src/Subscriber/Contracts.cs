using System.Text.Json.Serialization;
using Blumchen.Serialization;

namespace Subscriber
{
    [MessageRoutedByUrn("user-created:v1")]
    public record UserCreatedContract(
        Guid Id,
        string Name
    );

    [RawRoutedByUrn("user-deleted:v1")]
    public class MessageObjects;


    [RawRoutedByUrn("user-modified:v1")] 
    internal class MessageString;

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(UserCreatedContract))]
    internal partial class SourceGenerationContext: JsonSerializerContext;
}

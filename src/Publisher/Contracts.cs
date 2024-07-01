using System.Text.Json.Serialization;
using Blumchen.Serialization;

namespace Publisher;
public interface IContract{}

[MessageUrn("user-created:v1")]
internal record UserCreated(
    Guid Id,
    string Name = "Created"
):IContract;

[MessageUrn("user-deleted:v1")]
internal record UserDeleted(
    Guid Id,
    string Name = "Deleted"
): IContract;

[MessageUrn("user-modified:v1")] //subscription ignored
internal record UserModified(
    Guid Id,
    string Name = "Modified"
): IContract;


[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(UserCreated))]
[JsonSerializable(typeof(UserDeleted))]
[JsonSerializable(typeof(UserModified))]
internal partial class SourceGenerationContext: JsonSerializerContext;

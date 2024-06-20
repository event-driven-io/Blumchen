using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Blumchen.Streams;

namespace Blumchen.Serialization;
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

public static class JsonSerialization
{
    public static string ToJson<T>(T data, JsonTypeInfo typeInfo) where T:class=>
        JsonSerializer.Serialize(data, typeInfo);

    public static async ValueTask<object> FromJsonAsync(Type type, Stream stream, JsonSerializerContext context, CancellationToken ct = default)
        => await JsonSerializer.DeserializeAsync(stream.ToSohSkippingStream(), type, context, ct).ConfigureAwait(false)
           ?? throw new InvalidOperationException();
}

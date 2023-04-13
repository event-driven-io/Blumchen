using System.Text.Json;
using PostgresOutbox.Streams;

namespace PostgresOutbox.Serialization;

public static class JsonSerialization
{
    public static string ToJson(object data, JsonSerializerOptions? options = null) =>
        JsonSerializer.Serialize(data, options ?? new JsonSerializerOptions());

    public static ValueTask<object?> FromJsonAsync(Type type, Stream stream, CancellationToken ct = default) =>
        JsonSerializer.DeserializeAsync(stream.ToSOHSkippingStream(), type, new JsonSerializerOptions(), ct);
}

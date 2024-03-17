using System.Data.Common;
using System.Text.Json;
using Npgsql;
using PostgresOutbox.Streams;

namespace PostgresOutbox.Serialization;

public static class JsonSerialization
{
    private static readonly JsonSerializerOptions Options = new();

    public static string ToJson(object data, JsonSerializerOptions? options = null) =>
        JsonSerializer.Serialize(data, options ?? Options);

    public static ValueTask<object?> FromJsonAsync(Type type, Stream stream, CancellationToken ct = default) =>
        JsonSerializer.DeserializeAsync(stream.ToSohSkippingStream(), type, Options, ct);

}

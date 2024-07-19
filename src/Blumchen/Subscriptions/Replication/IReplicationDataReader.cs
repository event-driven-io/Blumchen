using Blumchen.Serialization;
using Npgsql;
using Npgsql.Replication.PgOutput;

namespace Blumchen.Subscriptions.Replication;

internal interface IReplicationDataReader<T>
{
    Task<T> Read(ReplicationValue replicationValue,  CancellationToken ct, Type? type = default);
    Task<T> Read(NpgsqlDataReader reader, CancellationToken ct, Type? type = default);
}
internal class ObjectReplicationDataReader: IReplicationDataReader<object>
{
    public Task<object> Read(ReplicationValue replicationValue, CancellationToken ct, Type? type = default)
        => replicationValue.Get<object>(ct).AsTask();


    public Task<object> Read(NpgsqlDataReader reader, CancellationToken ct, Type? type = default)
        => reader.GetFieldValueAsync<object>(2, ct);
}

internal class StringReplicationDataReader: IReplicationDataReader<string>
{
    public async Task<string> Read(ReplicationValue replicationValue, CancellationToken ct, Type? type = default)
    {
        using var tr = replicationValue.GetTextReader();
        return await tr.ReadToEndAsync(ct).ConfigureAwait(false);
    }


    public async Task<string> Read(NpgsqlDataReader reader, CancellationToken ct, Type? type = default)
    {
        using var tr = await reader.GetTextReaderAsync(2, ct).ConfigureAwait(false);
        return await tr.ReadToEndAsync(ct).ConfigureAwait(false);
    }
}

internal class JsonReplicationDataReader(JsonTypeResolver resolver): IReplicationDataReader<object>
{
    public async Task<object> Read(ReplicationValue replicationValue, CancellationToken ct, Type? type = default)
    {
        ArgumentNullException.ThrowIfNull(type);
        await using var stream = replicationValue.GetStream();
        return await JsonSerialization.FromJsonAsync(type, stream, resolver.SerializationContext, ct)
            .ConfigureAwait(false);
    }

    public async Task<object> Read(NpgsqlDataReader reader, CancellationToken ct, Type? type = default)
    {
        ArgumentNullException.ThrowIfNull(type);
        var stream = await reader.GetStreamAsync(2, ct).ConfigureAwait(false);
        await using var stream1 = stream.ConfigureAwait(false);
        return await JsonSerialization.FromJsonAsync(type, stream, resolver.SerializationContext, ct)
            .ConfigureAwait(false);
    }
}

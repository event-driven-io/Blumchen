using Npgsql;
using Npgsql.Replication.PgOutput.Messages;
using PostgresOutbox.Database;

namespace PostgresOutbox.Subscriptions.Replication;

public interface IReplicationDataMapper
{
    Task<object> ReadFromSnapshot(NpgsqlDataReader reader, CancellationToken ct);

    Task<object> ReadFromReplication(InsertMessage insertMessage, CancellationToken ct);
}

public interface IReplicationDataMapper<T>: IReplicationDataMapper where T : notnull
{
    new Task<T> ReadFromSnapshot(NpgsqlDataReader reader, CancellationToken ct);

    new Task<T> ReadFromReplication(InsertMessage insertMessage, CancellationToken ct);

    async Task<object> IReplicationDataMapper.ReadFromSnapshot(NpgsqlDataReader reader, CancellationToken ct) =>
        await ReadFromSnapshot(reader, ct);

    async Task<object> IReplicationDataMapper.ReadFromReplication(InsertMessage message, CancellationToken ct) =>
        await ReadFromReplication(message, ct);
}

public class DictionaryReplicationDataMapper<T>: IReplicationDataMapper<T> where T : notnull
{
    private readonly Func<IDictionary<string, object>, CancellationToken, ValueTask<T>> map;

    public DictionaryReplicationDataMapper(Func<IDictionary<string, object>, CancellationToken, ValueTask<T>> map) =>
        this.map = map;

    public async Task<T> ReadFromSnapshot(NpgsqlDataReader reader, CancellationToken ct) =>
        await map(await reader.ToDictionary(ct), ct);

    public async Task<T> ReadFromReplication(InsertMessage message, CancellationToken ct) =>
        await map(await message.ToDictionary(ct), ct);
}

public class DictionaryReplicationDataMapper: DictionaryReplicationDataMapper<object>
{
    public static DictionaryReplicationDataMapper<T> For<T>(
        Func<IDictionary<string, object>, CancellationToken, ValueTask<T>> map) where T : notnull => new(map);

    public static DictionaryReplicationDataMapper For(
        Func<IDictionary<string, object>, CancellationToken, ValueTask<object>> map) => new(map);

    public DictionaryReplicationDataMapper(Func<IDictionary<string, object>, CancellationToken, ValueTask<object>> map):
        base(map)
    {
    }
}

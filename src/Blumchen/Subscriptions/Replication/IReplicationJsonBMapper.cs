using Blumchen.Serialization;
using Npgsql;
using Npgsql.Replication.PgOutput;
using System.Text.Json.Serialization.Metadata;
using System.Text.Json;

namespace Blumchen.Subscriptions.Replication;

public interface IReplicationJsonBMapper
{
    Task<IEnvelope> ReadFromReplication(string id, string typeName, ReplicationValue column,
        CancellationToken ct);

    Task<IEnvelope> ReadFromSnapshot(string typeName, long id, NpgsqlDataReader reader, CancellationToken ct);
}
internal class ReplicationDataMapper<T, TU>(
    IReplicationDataReader<TU> replicationDataReader
    , ITypeResolver<T>? resolver = default
    ): IReplicationJsonBMapper
{
    public async Task<IEnvelope> ReadFromReplication(string id, string typeName, ReplicationValue column,
        CancellationToken ct)
    {

        try
        {
            var type = resolver?.Resolve(typeName);
            var value = await replicationDataReader.Read(column, ct, type).ConfigureAwait(false) ??
                        throw new ArgumentNullException();
            return new OkEnvelope(value, typeName);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or InvalidOperationException
                                       or JsonException)
        {
            return new KoEnvelope(ex, id);
        }
    }

    public async Task<IEnvelope> ReadFromSnapshot(string typeName, long id, NpgsqlDataReader reader, CancellationToken ct)
    {
        try
        {
            var eventType = resolver?.Resolve(typeName);
            ArgumentNullException.ThrowIfNull(eventType, typeName);
            var value = await replicationDataReader.Read(reader, ct, eventType).ConfigureAwait(false) ?? throw new ArgumentNullException();
            return new OkEnvelope(value, typeName);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or InvalidOperationException or JsonException)
        {
            return new KoEnvelope(ex, id.ToString());
        }
    }
}

internal sealed class ObjectReplicationDataMapper(
    IReplicationDataReader<object> replicationDataReader
): ReplicationDataMapper<object, object>(replicationDataReader)
{
    private static readonly Lazy<ObjectReplicationDataMapper> Lazy = new(() => new(new ObjectReplicationDataReader()));
    public static ObjectReplicationDataMapper Instance => Lazy.Value;
}

internal sealed class StringReplicationDataMapper(
    IReplicationDataReader<string> replicationDataReader
): ReplicationDataMapper<string, string>(replicationDataReader)
{
    private static readonly Lazy<StringReplicationDataMapper> Lazy = new(() => new(new StringReplicationDataReader()));
    public static StringReplicationDataMapper Instance => Lazy.Value;
}

internal sealed class JsonReplicationDataMapper(
    ITypeResolver<JsonTypeInfo> resolver,
    IReplicationDataReader<object> replicationDataReader
): ReplicationDataMapper<JsonTypeInfo, object>(replicationDataReader, resolver);

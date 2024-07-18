using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Blumchen.Serialization;
using Npgsql;
using Npgsql.Replication.PgOutput;
using Npgsql.Replication.PgOutput.Messages;

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


internal class StringReplicationDataReader : IReplicationDataReader<string>
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

internal class ReplicationDataMapper(IDictionary<string, Tuple<IReplicationJsonBMapper, IMessageHandler>> mapperSelector)
    : IReplicationDataMapper
{
    public async Task<IEnvelope> ReadFromReplication(InsertMessage insertMessage, CancellationToken ct)
    {
        var id = string.Empty;
        var columnNumber = 0;
        var typeName = string.Empty;
        await foreach (var column in insertMessage.NewRow.ConfigureAwait(false))
        {
            try
            {
                switch (columnNumber)
                {
                    case 0:
                        id = column.Kind == TupleDataKind.BinaryValue
                            ? (await column.Get<long>(ct).ConfigureAwait(false)).ToString()
                            : await column.Get<string>(ct).ConfigureAwait(false);
                        break;
                    case 1:
                        using (var textReader = column.GetTextReader())
                        {
                            typeName = await textReader.ReadToEndAsync(ct).ConfigureAwait(false);
                            break;
                        }
                    case 2 when column.GetDataTypeName().Equals("jsonb", StringComparison.OrdinalIgnoreCase):
                        return await mapperSelector[typeName].Item1.ReadFromReplication(id, typeName, column, ct);
                }
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or InvalidOperationException or JsonException)
            {
                return new KoEnvelope(ex, id);
            }
            columnNumber++;
        }
        throw new InvalidOperationException("You should not get here");
    }

    public async Task<IEnvelope> ReadFromSnapshot(NpgsqlDataReader reader, CancellationToken ct)
    {
        long id = default;
        try
        {
            id = reader.GetInt64(0);
            var typeName = reader.GetString(1);

            return await mapperSelector[typeName].Item1.ReadFromSnapshot(typeName, id, reader, ct);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or InvalidOperationException or JsonException)
        {
            return new KoEnvelope(ex, id.ToString());
        }
    }
}

public interface IReplicationJsonBMapper
{
    Task<IEnvelope> ReadFromReplication(string id, string typeName, ReplicationValue column,
        CancellationToken ct);

    Task<IEnvelope> ReadFromSnapshot(string typeName, long id, NpgsqlDataReader reader, CancellationToken ct);
}

internal class ReplicationDataMapper<T,TU>(
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
): ReplicationDataMapper<JsonTypeInfo,object>(replicationDataReader, resolver);

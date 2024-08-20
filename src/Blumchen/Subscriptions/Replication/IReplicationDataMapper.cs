using System.Reflection;
using Npgsql;
using Npgsql.Replication.PgOutput;
using Npgsql.Replication.PgOutput.Messages;
using System.Text.Json;
using Blumchen.Subscriber;
using ImTools;

namespace Blumchen.Subscriptions.Replication;

public interface IReplicationDataMapper
{
    Task<IEnvelope> ReadFromSnapshot(NpgsqlDataReader reader, CancellationToken ct);

    Task<IEnvelope> ReadFromReplication(InsertMessage insertMessage, CancellationToken ct);
}

internal class ReplicationDataMapper(IDictionary<string, Tuple<IReplicationJsonBMapper, IMessageHandler, MethodInfo>> mapperSelector)
    : IReplicationDataMapper
{

    private ImHashMap<string, IReplicationJsonBMapper> _memo = ImHashMap<string, IReplicationJsonBMapper>.Empty;

    private static IReplicationJsonBMapper SelectMapper(string key, IDictionary<string, Tuple<IReplicationJsonBMapper, IMessageHandler, MethodInfo>> registry) =>
        registry.FindByMultiKey(key, OptionsBuilder.WildCard)?.Item1
        ?? throw new NotSupportedException($"Unexpected message `{key}`");

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

                        
                        _memo = _memo.AddOrGetEntry(typeName.GetHashCode(),
                            new KVEntry<string, IReplicationJsonBMapper>(typeName.GetHashCode(), typeName,
                                SelectMapper(typeName, mapperSelector)));
                        return await _memo.GetValueOrDefault(typeName.GetHashCode(), typeName)
                            .ReadFromReplication(id, typeName, column, ct);

                }
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or InvalidOperationException
                                           or JsonException)
            {
                return new KoEnvelope(ex, id);
            }

            columnNumber++;
        }

        throw new InvalidOperationException("You should not get here");
    }

    private IReplicationJsonBMapper Get(string typeName)
    {
        if (_memo.TryFind(typeName, out var mapper)) return mapper;
        mapper = SelectMapper(typeName, mapperSelector);
        _memo = _memo.AddOrUpdate(typeName, mapper);
        return mapper;
    }

    public async Task<IEnvelope> ReadFromSnapshot(NpgsqlDataReader reader, CancellationToken ct)
    {
        long id = default;
        try
        {
            id = reader.GetInt64(0);
            var typeName = reader.GetString(1);
            var mapper = Get(typeName);
            return await mapper.ReadFromSnapshot(typeName, id, reader, ct);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or InvalidOperationException
                                       or JsonException)
        {
            return new KoEnvelope(ex, id.ToString());
        }
    }
}

using System.Reflection;
using Npgsql;
using Npgsql.Replication.PgOutput;
using Npgsql.Replication.PgOutput.Messages;
using System.Text.Json;
using Blumchen.Subscriber;

namespace Blumchen.Subscriptions.Replication;

public interface IReplicationDataMapper
{
    Task<IEnvelope> ReadFromSnapshot(NpgsqlDataReader reader, CancellationToken ct);

    Task<IEnvelope> ReadFromReplication(InsertMessage insertMessage, CancellationToken ct);
}

internal class ReplicationDataMapper(IDictionary<string, Tuple<IReplicationJsonBMapper, IMessageHandler, MethodInfo>> mapperSelector)
    : IReplicationDataMapper
{
    private readonly Func<string, IDictionary<string, Tuple<IReplicationJsonBMapper, IMessageHandler, MethodInfo>>, IReplicationJsonBMapper> _memoizer = Memoizer<string, IDictionary<string, Tuple<IReplicationJsonBMapper, IMessageHandler, MethodInfo>>, IReplicationJsonBMapper>.Execute(SelectMapper);

    private static IReplicationJsonBMapper SelectMapper(string key, IDictionary<string, Tuple<IReplicationJsonBMapper, IMessageHandler, MethodInfo>> registry)
        => registry.FindByMultiKey(key, OptionsBuilder.WildCard)?.Item1
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

                        return await _memoizer(typeName, mapperSelector).ReadFromReplication(id, typeName, column, ct);
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

    public async Task<IEnvelope> ReadFromSnapshot(NpgsqlDataReader reader, CancellationToken ct)
    {
        long id = default;
        try
        {
            id = reader.GetInt64(0);
            var typeName = reader.GetString(1);
            return await _memoizer(typeName, mapperSelector).ReadFromSnapshot(typeName, id, reader, ct);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or InvalidOperationException or JsonException)
        {
            return new KoEnvelope(ex, id.ToString());
        }
    }
}


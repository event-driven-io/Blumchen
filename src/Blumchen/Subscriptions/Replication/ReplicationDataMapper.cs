using System.Text.Json;
using Blumchen.Serialization;
using Blumchen.Subscriptions.ReplicationMessageHandlers;
using Npgsql;
using Npgsql.Replication.PgOutput;
using Npgsql.Replication.PgOutput.Messages;

namespace Blumchen.Subscriptions.Replication;

internal sealed class ReplicationDataMapper(JsonTypeResolver resolver): IReplicationDataMapper
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
                    {
                        var type = resolver.Resolve(typeName);
                        ArgumentNullException.ThrowIfNull(type, typeName);
                        return new OkEnvelope(await JsonSerialization.FromJsonAsync(type, column.GetStream(), resolver.SerializationContext, ct).ConfigureAwait(false));
                    }
                }
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or InvalidOperationException or JsonException)
            {
                return new KoEnvelope(ex,id);
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
            var eventTypeName = reader.GetString(1);
            var eventType = resolver.Resolve(eventTypeName);
            ArgumentNullException.ThrowIfNull(eventType, eventTypeName);
            var stream = await reader.GetStreamAsync(2, ct).ConfigureAwait(false);
            await using var stream1 = stream.ConfigureAwait(false);
            return new OkEnvelope(await JsonSerialization.FromJsonAsync(eventType, stream, resolver.SerializationContext, ct).ConfigureAwait(false));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or InvalidOperationException or JsonException)
        {
            return new KoEnvelope(ex, id.ToString());
        }
    }
}

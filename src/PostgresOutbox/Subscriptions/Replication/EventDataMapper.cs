using Npgsql;
using Npgsql.Replication.PgOutput.Messages;
using PostgresOutbox.Serialization;
using PostgresOutbox.Subscriptions.ReplicationMessageHandlers;
using System.Text.Json;

namespace PostgresOutbox.Subscriptions.Replication;

internal sealed class EventDataMapper(ITypeResolver resolver): IReplicationDataMapper
{
    public async Task<IEnvelope> ReadFromReplication(InsertMessage insertMessage, CancellationToken ct)
    {
        var id = string.Empty;
        var columnNumber = 0;
        var eventTypeName = string.Empty;

        await foreach (var column in insertMessage.NewRow)
        {
            try
            {
                switch (columnNumber)
                {
                    case 0:
                        id = await column.Get<string>(ct);
                        break;
                    case 1:
                        eventTypeName = await column.GetTextReader().ReadToEndAsync(ct);
                        break;
                    case 2 when column.GetDataTypeName().Equals("jsonb", StringComparison.OrdinalIgnoreCase):
                    {
                        var eventType = resolver.Resolve(eventTypeName);
                        ArgumentNullException.ThrowIfNull(eventType, eventTypeName);
                        return new OkEnvelope(await JsonSerialization.FromJsonAsync(eventType, column.GetStream(), resolver.SerializationContext, ct));
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

            var stream = await reader.GetStreamAsync(2, ct);
            ArgumentNullException.ThrowIfNull(eventType, eventTypeName);
            return new OkEnvelope(await JsonSerialization.FromJsonAsync(eventType, stream, resolver.SerializationContext, ct));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or InvalidOperationException or JsonException)
        {
            return new KoEnvelope(ex, id.ToString());
        }
    }
}

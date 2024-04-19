using Npgsql;
using Npgsql.Replication.PgOutput;
using Npgsql.Replication.PgOutput.Messages;
using PostgresOutbox.Serialization;

namespace PostgresOutbox.Subscriptions.Replication;

internal sealed class EventDataMapper(ITypeResolver resolver): IReplicationDataMapper
{
    public async Task<object> ReadFromReplication(InsertMessage insertMessage, CancellationToken ct)
    {
        var columnNumber = 0;
        var eventTypeName = string.Empty;

        await foreach (var value in insertMessage.NewRow)
        {
            switch (columnNumber)
            {
                case 1:
                    eventTypeName = await value.GetTextReader().ReadToEndAsync(ct);
                    break;
                case 2 when value.GetDataTypeName().Equals("jsonb", StringComparison.OrdinalIgnoreCase):
                {
                    var eventType = resolver.Resolve(eventTypeName);
                    ArgumentNullException.ThrowIfNull(eventType, eventTypeName);
                    var @event = await JsonSerialization.FromJsonAsync(eventType, value.GetStream(), resolver.SerializationContext, ct);

                    return @event!;
                }
            }

            columnNumber++;
        }

        throw new InvalidOperationException("You should not get here");
    }

    public async Task<object> ReadFromSnapshot(NpgsqlDataReader reader, CancellationToken ct)
    {
        var eventTypeName = reader.GetString(1);
        var eventType = resolver.Resolve(eventTypeName);

        var stream = await reader.GetStreamAsync(2, ct);
        ArgumentNullException.ThrowIfNull(eventType, eventTypeName);
        var @event = await JsonSerialization.FromJsonAsync(eventType, stream, resolver.SerializationContext, ct);

        return @event!;
    }
}

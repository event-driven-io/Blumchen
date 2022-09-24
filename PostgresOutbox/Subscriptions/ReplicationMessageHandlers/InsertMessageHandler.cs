using Npgsql.Replication.PgOutput.Messages;
using PostgresOutbox.Serialization;

namespace PostgresOutbox.Subscriptions.ReplicationMessageHandlers;

public static class InsertMessageHandler
{
    public static async Task<object> Handle(InsertMessage message, CancellationToken ct)
    {
        var columnNumber = 0;
        var eventTypeName = string.Empty;

        await foreach (var value in message.NewRow)
        {
            switch (columnNumber)
            {
                case 1:
                    eventTypeName = await value.GetTextReader().ReadToEndAsync();
                    break;
                case 2 when value.GetDataTypeName().ToLower() == "jsonb":
                {
                    var eventType = Type.GetType(eventTypeName);
                    if (eventType is null)
                        throw new ArgumentOutOfRangeException(nameof(eventType));

                    var @event = await JsonSerialization.FromJsonAsync(eventType, value.GetStream(), ct);

                    return @event!;
                }
            }

            columnNumber++;
        }

        throw new InvalidOperationException("You should not get here");
    }
}

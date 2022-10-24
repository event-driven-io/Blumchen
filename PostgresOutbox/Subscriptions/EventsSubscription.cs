using System.Runtime.CompilerServices;
using Npgsql.Replication;
using Npgsql.Replication.PgOutput;
using Npgsql.Replication.PgOutput.Messages;
using PostgresOutbox.Subscriptions.ReplicationMessageHandlers;

namespace PostgresOutbox.Subscriptions;

public record EventsSubscriptionOptions(
    string ConnectionString,
    string SlotName,
    string PublicationName
);

public interface IEventsSubscription
{
    IAsyncEnumerable<object> Subscribe(EventsSubscriptionOptions options, CancellationToken ct);
}

public class EventsSubscription: IEventsSubscription
{
    public async IAsyncEnumerable<object> Subscribe(EventsSubscriptionOptions options,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var (connectionString, slotName, publicationName) = options;
        await using var conn = new LogicalReplicationConnection(connectionString);
        await conn.Open(ct);

        var slot = new PgOutputReplicationSlot(slotName);

        await foreach (var message in conn.StartReplication(slot, new PgOutputReplicationOptions(publicationName, 1),
                           ct))
        {
            if (message is InsertMessage insertMessage)
            {
                yield return await InsertMessageHandler.Handle(insertMessage, ct);
            }

            // Always call SetReplicationStatus() or assign LastAppliedLsn and LastFlushedLsn individually
            // so that Npgsql can inform the server which WAL files can be removed/recycled.
            conn.SetReplicationStatus(message.WalEnd);
            await conn.SendStatusUpdate(ct);
        }
    }
}

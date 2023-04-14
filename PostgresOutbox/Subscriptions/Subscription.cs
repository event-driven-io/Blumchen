using System.Runtime.CompilerServices;
using Npgsql;
using Npgsql.Replication;
using Npgsql.Replication.PgOutput;
using Npgsql.Replication.PgOutput.Messages;
using NpgsqlTypes;
using PostgresOutbox.Subscriptions.Management;
using PostgresOutbox.Subscriptions.Replication;
using PostgresOutbox.Subscriptions.ReplicationMessageHandlers;
using PostgresOutbox.Subscriptions.SnapshotReader;

namespace PostgresOutbox.Subscriptions;

using static SubscriptionManagement;
using static SubscriptionManagement.CreateReplicationSlotResult;

public interface ISubscription
{
    IAsyncEnumerable<object> Subscribe(SubscriptionOptions options, CancellationToken ct);
}

public record SubscriptionOptions(
    string ConnectionString,
    string SlotName,
    string PublicationName,
    string TableName,
    IReplicationDataMapper DataMapper,
    CreateStyle CreateStyle = CreateStyle.WhenNotExists
);

public enum CreateStyle
{
    WhenNotExists,
    AlwaysRecreate,
    Never
}

public class Subscription: ISubscription
{
    public async IAsyncEnumerable<object> Subscribe(
        SubscriptionOptions options,
        [EnumeratorCancellation] CancellationToken ct = default
    )
    {
        var (connectionString, slotName, publicationName, _, _, _) = options;

        await using var conn = new LogicalReplicationConnection(connectionString);
        await conn.Open(ct);

        var result = await CreateSubscription(conn, options, ct);

        PgOutputReplicationSlot slot;

        if (result is not Created created)
        {
            slot = new PgOutputReplicationSlot(slotName);
        }
        else
        {
            slot = new PgOutputReplicationSlot(new ReplicationSlotOptions(slotName, created.LogSequenceNumber));
            await foreach (var @event in ReadExistingRowsFromSnapshot(created.SnapshotName, options, ct))
            {
                yield return @event;
            }
        }

        await foreach (var message in
                       conn.StartReplication(slot, new PgOutputReplicationOptions(publicationName, 1), ct))
        {
            if (message is InsertMessage insertMessage)
            {
                yield return await InsertMessageHandler.Handle(insertMessage, options.DataMapper, ct);
            }

            // Always call SetReplicationStatus() or assign LastAppliedLsn and LastFlushedLsn individually
            // so that Npgsql can inform the server which WAL files can be removed/recycled.
            conn.SetReplicationStatus(message.WalEnd);
            await conn.SendStatusUpdate(ct);
        }
    }

    private static async IAsyncEnumerable<object> ReadExistingRowsFromSnapshot(
        string snapshotName,
        SubscriptionOptions options,
        [EnumeratorCancellation] CancellationToken ct = default
    )
    {
        await using var connection = new NpgsqlConnection(options.ConnectionString);
        await connection.OpenAsync(ct);

        await foreach (var row in connection.GetRowsFromSnapshot(snapshotName, options.TableName, options.DataMapper,
                           ct))
        {
            yield return row;
        }
    }
}

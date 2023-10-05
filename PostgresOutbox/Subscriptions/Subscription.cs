using System.Runtime.CompilerServices;
using Npgsql;
using Npgsql.Replication;
using Npgsql.Replication.PgOutput;
using Npgsql.Replication.PgOutput.Messages;
using PostgresOutbox.Subscriptions.Management;
using PostgresOutbox.Subscriptions.Replication;
using PostgresOutbox.Subscriptions.ReplicationMessageHandlers;
using PostgresOutbox.Subscriptions.SnapshotReader;

namespace PostgresOutbox.Subscriptions;

using static PublicationManagement;
using static ReplicationSlotManagement;
using static ReplicationSlotManagement.CreateReplicationSlotResult;

public interface ISubscription
{
    IAsyncEnumerable<object> Subscribe(SubscriptionOptions options, CancellationToken ct);
}

public record SubscriptionOptions(
    string ConnectionString,
    PublicationSetupOptions PublicationSetupOptions,
    ReplicationSlotSetupOptions SlotSetupOptions,
    IReplicationDataMapper DataMapper
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
        var (connectionString, publicationSetupOptions, slotSetupOptions, replicationDataMapper) = options;
        var dataSource = NpgsqlDataSource.Create(connectionString);

        await using var conn = new LogicalReplicationConnection(connectionString);
        await conn.Open(ct);

        await dataSource.SetupPublication(publicationSetupOptions, ct);
        var result = await dataSource.SetupReplicationSlot(conn, slotSetupOptions, ct);

        PgOutputReplicationSlot slot;

        if (result is not Created created)
        {
            slot = new PgOutputReplicationSlot(slotSetupOptions.SlotName);
        }
        else
        {
            slot = new PgOutputReplicationSlot(
                new ReplicationSlotOptions(
                    slotSetupOptions.SlotName,
                    created.LogSequenceNumber
                )
            );

            await foreach (var @event in ReadExistingRowsFromSnapshot(dataSource, created.SnapshotName, options, ct))
            {
                yield return @event;
            }
        }

        await foreach (var message in
                       conn.StartReplication(slot,
                           new PgOutputReplicationOptions(publicationSetupOptions.PublicationName, 1), ct))
        {
            if (message is InsertMessage insertMessage)
            {
                yield return await InsertMessageHandler.Handle(insertMessage, replicationDataMapper, ct);
            }

            // Always call SetReplicationStatus() or assign LastAppliedLsn and LastFlushedLsn individually
            // so that Npgsql can inform the server which WAL files can be removed/recycled.
            conn.SetReplicationStatus(message.WalEnd);
            await conn.SendStatusUpdate(ct);
        }
    }

    private static async IAsyncEnumerable<object> ReadExistingRowsFromSnapshot(
        NpgsqlDataSource dataSource,
        string snapshotName,
        SubscriptionOptions options,
        [EnumeratorCancellation] CancellationToken ct = default
    )
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);

        await foreach (var row in connection.GetRowsFromSnapshot(
                           snapshotName,
                           options.PublicationSetupOptions.TableName,
                           options.DataMapper,
                           ct))
        {
            yield return row;
        }
    }
}

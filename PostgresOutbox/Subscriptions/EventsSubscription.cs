using System.Runtime.CompilerServices;
using Npgsql;
using Npgsql.Replication;
using Npgsql.Replication.PgOutput;
using Npgsql.Replication.PgOutput.Messages;
using PostgresOutbox.Database;
using PostgresOutbox.Subscriptions.Replication;
using PostgresOutbox.Subscriptions.ReplicationMessageHandlers;
using PostgresOutbox.Subscriptions.SnapshotReader;

namespace PostgresOutbox.Subscriptions;

using static EventsSubscription.CreateReplicationSlotResult;

public record EventsSubscriptionOptions(
    string ConnectionString,
    string SlotName,
    string PublicationName,
    string TableName,
    IReplicationDataMapper DataMapper
);



public interface IEventsSubscription
{
    IAsyncEnumerable<object> Subscribe(EventsSubscriptionOptions options, CancellationToken ct);
}

public class EventsSubscription: IEventsSubscription
{
    private async Task<CreateReplicationSlotResult> CreateSubscription(
        LogicalReplicationConnection connection,
        EventsSubscriptionOptions options,
        CancellationToken ct
    )
    {
        if (!await PublicationExists(options, ct))
            await CreatePublication(options, ct);

        if (await ReplicationSlotExists(options, ct))
            return new AlreadyExists();

        var result = await connection.CreatePgOutputReplicationSlot(options.SlotName,
            slotSnapshotInitMode: LogicalSlotSnapshotInitMode.Export, cancellationToken: ct);

        return new Created(options.TableName, result.SnapshotName!);
    }

    public async IAsyncEnumerable<object> Subscribe(
        EventsSubscriptionOptions options,
        [EnumeratorCancellation] CancellationToken ct = default
    )
    {
        var (connectionString, slotName, publicationName, _, _) = options;
        await using var conn = new LogicalReplicationConnection(connectionString);
        await conn.Open(ct);

        var result = await CreateSubscription(conn, options, ct);

        if (result is Created created)
        {
            await foreach (var @event in ReadExistingEventsFromSnapshot(created.SnapshotName, options, ct))
            {
                yield return @event;
            }
        }

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

    private async Task<bool> ReplicationSlotExists(
        EventsSubscriptionOptions options,
        CancellationToken ct
    )
    {
        var (connectionString, slotName, _, _, _) = options;
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        return await dataSource.Exists("pg_replication_slots", "slot_name = $1", new object[] { slotName }, ct);
    }

    private async Task CreatePublication(
        EventsSubscriptionOptions options,
        CancellationToken ct
    )
    {
        var (connectionString, _, publicationName, tableName, _) = options;
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await dataSource.Execute($"CREATE PUBLICATION {publicationName} FOR TABLE {tableName};", ct);
    }

    private async Task<bool> PublicationExists(
        EventsSubscriptionOptions options,
        CancellationToken ct
    )
    {
        var (connectionString, _, publicationName, _, _) = options;
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        return await dataSource.Exists("pg_publication", "pubname = $1", new object[] { publicationName }, ct);
    }

    private async IAsyncEnumerable<object> ReadExistingEventsFromSnapshot(
        string snapshotName,
        EventsSubscriptionOptions options,

        [EnumeratorCancellation] CancellationToken ct = default
    )
    {
        await using var connection = new NpgsqlConnection(options.ConnectionString);
        await connection.OpenAsync(ct);

        await foreach (var @event in connection.GetEventsFromSnapshot(snapshotName, options.TableName, options.DataMapper, ct))
        {
            yield return @event;
        }
    }

    internal abstract record CreateReplicationSlotResult
    {
        public record AlreadyExists: CreateReplicationSlotResult;

        public record Created(string TableName, string SnapshotName): CreateReplicationSlotResult;
    }
}

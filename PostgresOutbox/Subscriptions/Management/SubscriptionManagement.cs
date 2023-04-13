using Npgsql;
using Npgsql.Replication;
using PostgresOutbox.Database;

namespace PostgresOutbox.Subscriptions.Management;

public static class SubscriptionManagement
{
    public static async Task<CreateReplicationSlotResult> CreateSubscription(
        LogicalReplicationConnection connection,
        SubscriptionOptions options,
        CancellationToken ct
    )
    {
        if (!await PublicationExists(options, ct))
            await CreatePublication(options, ct);

        if (await ReplicationSlotExists(options, ct))
            return new CreateReplicationSlotResult.AlreadyExists();

        var result = await connection.CreatePgOutputReplicationSlot(
            options.SlotName,
            slotSnapshotInitMode: LogicalSlotSnapshotInitMode.Export,
            cancellationToken: ct
        );

        return new CreateReplicationSlotResult.Created(options.TableName, result.SnapshotName!);
    }

    private static async Task<bool> ReplicationSlotExists(
        SubscriptionOptions options,
        CancellationToken ct
    )
    {
        var (connectionString, slotName, _, _, _) = options;
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        return await dataSource.Exists("pg_replication_slots", "slot_name = $1", new object[] { slotName }, ct);
    }

    private static async Task CreatePublication(
        SubscriptionOptions options,
        CancellationToken ct
    )
    {
        var (connectionString, _, publicationName, tableName, _) = options;
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await dataSource.Execute($"CREATE PUBLICATION {publicationName} FOR TABLE {tableName};", ct);
    }

    private static async Task<bool> PublicationExists(
        SubscriptionOptions options,
        CancellationToken ct
    )
    {
        var (connectionString, _, publicationName, _, _) = options;
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        return await dataSource.Exists("pg_publication", "pubname = $1", new object[] { publicationName }, ct);
    }

    public abstract record CreateReplicationSlotResult
    {
        public record AlreadyExists: CreateReplicationSlotResult;

        public record Created(string TableName, string SnapshotName): CreateReplicationSlotResult;
    }
}

using Npgsql;
using Npgsql.Replication;
using NpgsqlTypes;
using PostgresOutbox.Database;

namespace PostgresOutbox.Subscriptions.Management;
using static ReplicationSlotManagement.CreateReplicationSlotResult;

public static class ReplicationSlotManagement
{
    public static async Task<CreateReplicationSlotResult> SetupReplicationSlot(
        this NpgsqlDataSource dataSource,
        LogicalReplicationConnection connection,
        ReplicationSlotSetupOptions options,
        CancellationToken ct
    )
    {
        var (slotName, createStyle) = options;

        if(createStyle == CreateStyle.Never)
            return new None();

        if (await dataSource.ReplicationSlotExists(slotName, ct))
        {
            if (createStyle == CreateStyle.WhenNotExists)
                return new AlreadyExists();

            await connection.DropReplicationSlot(slotName, true, ct);
        }

        var result = await connection.CreatePgOutputReplicationSlot(
            slotName,
            slotSnapshotInitMode: LogicalSlotSnapshotInitMode.Export,
            cancellationToken: ct
        );

        return new Created(result.SnapshotName!, result.ConsistentPoint);
    }

    private static Task<bool> ReplicationSlotExists(
        this NpgsqlDataSource dataSource,
        string slotName,
        CancellationToken ct
    ) =>
        dataSource.Exists("pg_replication_slots", "slot_name = $1", new object[] { slotName }, ct);

    public record ReplicationSlotSetupOptions(
        string SlotName,
        CreateStyle CreateStyle = CreateStyle.WhenNotExists
    );

    public abstract record CreateReplicationSlotResult
    {
        public record None: CreateReplicationSlotResult;

        public record AlreadyExists: CreateReplicationSlotResult;

        public record Created(string SnapshotName, NpgsqlLogSequenceNumber LogSequenceNumber): CreateReplicationSlotResult;
    }
}

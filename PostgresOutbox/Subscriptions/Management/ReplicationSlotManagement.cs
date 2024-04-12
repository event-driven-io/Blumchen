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

        return (createStyle, await dataSource.ReplicationSlotExists(slotName, ct)) switch
        {
            (CreateStyle.Never,_) => new None(),
            (CreateStyle.WhenNotExists,true) => new AlreadyExists(),
            (CreateStyle.WhenNotExists,false) => await Create(connection, slotName, ct),
            (CreateStyle.AlwaysRecreate,true) => await ReCreate(connection, slotName, ct),
            (CreateStyle.AlwaysRecreate, false) => await Create(connection, slotName, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(options.CreateStyle))
        };

        static async Task<CreateReplicationSlotResult> ReCreate(
            LogicalReplicationConnection connection,
            string slotName,
            CancellationToken ct)
        {
            await connection.DropReplicationSlot(slotName, true, ct);
            return await Create(connection, slotName, ct);
        }

        static async Task<CreateReplicationSlotResult> Create(
            LogicalReplicationConnection connection,
            string slotName,
            CancellationToken ct)
        {
            var result = await connection.CreatePgOutputReplicationSlot(
                slotName,
                slotSnapshotInitMode: LogicalSlotSnapshotInitMode.Export,
                cancellationToken: ct
            );

            return new Created(result.SnapshotName!, result.ConsistentPoint);
        }
    }

    private static Task<bool> ReplicationSlotExists(
        this NpgsqlDataSource dataSource,
        string slotName,
        CancellationToken ct
    ) =>
        dataSource.Exists("pg_replication_slots", "slot_name = $1", [slotName], ct);

    public record ReplicationSlotSetupOptions(
        string SlotName = $"{PublicationManagement.PublicationSetupOptions.DefaultTableName}_slot",
        CreateStyle CreateStyle = CreateStyle.WhenNotExists
    );

    public abstract record CreateReplicationSlotResult
    {
        public record None: CreateReplicationSlotResult;

        public record AlreadyExists: CreateReplicationSlotResult;

        public record Created(string SnapshotName, NpgsqlLogSequenceNumber LogSequenceNumber): CreateReplicationSlotResult;
    }
}

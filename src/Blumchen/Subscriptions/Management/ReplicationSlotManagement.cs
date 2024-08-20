using Blumchen.Database;
using Npgsql;
using Npgsql.Replication;
using NpgsqlTypes;

namespace Blumchen.Subscriptions.Management;
using static ReplicationSlotManagement.CreateReplicationSlotResult;

public static class ReplicationSlotManagement
{
    #pragma warning disable CA2208
    private static Task<bool> ReplicationSlotExists(
        this NpgsqlDataSource dataSource,
        string slotName,
        CancellationToken ct
    ) => dataSource.Exists("pg_replication_slots", "slot_name ILIKE $1", [slotName], ct);

    public static async Task<CreateReplicationSlotResult> SetupReplicationSlot(
        this NpgsqlDataSource dataSource,
        LogicalReplicationConnection connection,
        ReplicationSlotOptions options,
        CancellationToken ct
    )
    {
        var (slotName, createStyle, _) = options;

        return (createStyle, await dataSource.ReplicationSlotExists(slotName, ct).ConfigureAwait(false)) switch
        {
            (Subscription.CreateStyle.Never,_) => new None(),
            (Subscription.CreateStyle.WhenNotExists,true) => new AlreadyExists(),
            (Subscription.CreateStyle.WhenNotExists,false) => await connection.Create(slotName, ct).ConfigureAwait(false),
            (Subscription.CreateStyle.AlwaysRecreate,true) => await connection.ReCreate(slotName, ct).ConfigureAwait(false),
            (Subscription.CreateStyle.AlwaysRecreate, false) => await connection.Create(slotName, ct).ConfigureAwait(false),

            _ => throw new ArgumentOutOfRangeException(nameof(options.CreateStyle))
        };

    }
    
    public record ReplicationSlotOptions(
        string SlotName = $"{TableDescriptorBuilder.MessageTable.DefaultName}_slot",
        Subscription.CreateStyle CreateStyle = Subscription.CreateStyle.WhenNotExists,
        bool Binary =
            false //https://www.postgresql.org/docs/current/sql-createsubscription.html#SQL-CREATESUBSCRIPTION-WITH-BINARY
    );
    public abstract record CreateReplicationSlotResult
    {
        public record None: CreateReplicationSlotResult;

        public record AlreadyExists: CreateReplicationSlotResult;

        public record Created(string SnapshotName, NpgsqlLogSequenceNumber LogSequenceNumber): CreateReplicationSlotResult;
    }
}

internal static class LogicalReplicationConnectionExtensions
{
    internal static async Task<ReplicationSlotManagement.CreateReplicationSlotResult> Create(
        this LogicalReplicationConnection connection,
        string slotName,
        CancellationToken ct)
    {
        var result = await connection.CreatePgOutputReplicationSlot(
            slotName,
            slotSnapshotInitMode: LogicalSlotSnapshotInitMode.Export,
            cancellationToken: ct
        ).ConfigureAwait(false);

        return new Created(result.SnapshotName!, result.ConsistentPoint);
    }

    internal static async Task<ReplicationSlotManagement.CreateReplicationSlotResult> ReCreate(
        this LogicalReplicationConnection connection,
        string slotName,
        CancellationToken ct)
    {
        await connection.DropReplicationSlot(slotName, true, ct).ConfigureAwait(false);
        return await connection.Create(slotName, ct).ConfigureAwait(false);
    }
}

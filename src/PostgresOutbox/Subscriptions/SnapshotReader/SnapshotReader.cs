using System.Runtime.CompilerServices;
using Npgsql;
using PostgresOutbox.Database;
using PostgresOutbox.Subscriptions.Replication;
using PostgresOutbox.Subscriptions.ReplicationMessageHandlers;

namespace PostgresOutbox.Subscriptions.SnapshotReader;

public static class SnapshotReader
{
    internal static async IAsyncEnumerable<IEnvelope> GetRowsFromSnapshot(
        this NpgsqlConnection connection,
        string snapshotName,
        string tableName,
        IReplicationDataMapper dataMapper,
        [EnumeratorCancellation] CancellationToken ct = default
    )
    {
        await foreach (var @event in connection.QueryTransactionSnapshot(snapshotName, tableName, dataMapper, ct))
            yield return @event;
    }
}

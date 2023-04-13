using System.Runtime.CompilerServices;
using Npgsql;
using PostgresOutbox.Database;
using PostgresOutbox.Serialization;
using PostgresOutbox.Subscriptions.Replication;

namespace PostgresOutbox.Subscriptions.SnapshotReader;

public static class SnapshotReader
{
    public static async IAsyncEnumerable<object> GetRowsFromSnapshot(
        this NpgsqlConnection connection,
        string snapshotName,
        string tableName,
        IReplicationDataMapper dataMapper,
        [EnumeratorCancellation] CancellationToken ct = default
    )
    {
        await foreach (var @event in connection.QueryTransactionSnapshot(snapshotName, tableName, dataMapper, ct))
        {
            yield return @event;
        }
    }
}

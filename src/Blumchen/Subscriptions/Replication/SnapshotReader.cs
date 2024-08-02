using System.Runtime.CompilerServices;
using Blumchen.Database;
using Npgsql;

namespace Blumchen.Subscriptions.Replication;

public static class SnapshotReader
{
    internal static async IAsyncEnumerable<IEnvelope> GetRowsFromSnapshot(this NpgsqlConnection connection,
        string snapshotName,
        TableDescriptorBuilder.MessageTable tableDescriptor,
        IReplicationDataMapper dataMapper,
        ISet<string> registeredTypes,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var @event in connection.QueryTransactionSnapshot(
                           snapshotName,
                           tableDescriptor,
                           registeredTypes,
                           dataMapper,
                           ct).ConfigureAwait(false))
            yield return @event;
    }
}

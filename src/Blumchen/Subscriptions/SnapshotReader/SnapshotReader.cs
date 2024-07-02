using System.Runtime.CompilerServices;
using Blumchen.Database;
using Blumchen.Subscriptions.Replication;
using Blumchen.Subscriptions.ReplicationMessageHandlers;
using Npgsql;

namespace Blumchen.Subscriptions.SnapshotReader;
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

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

using Npgsql.Replication.PgOutput.Messages;
using PostgresOutbox.Subscriptions.Replication;

namespace PostgresOutbox.Subscriptions.ReplicationMessageHandlers;

public static class InsertMessageHandler
{
    public static async Task<object> Handle(
        InsertMessage message,
        IReplicationDataMapper dataMapper,
        CancellationToken ct
    ) =>
        await dataMapper.ReadFromReplication(message, ct);
}

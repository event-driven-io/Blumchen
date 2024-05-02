using Npgsql;
using Npgsql.Replication.PgOutput.Messages;
using PostgresOutbox.Subscriptions.ReplicationMessageHandlers;

namespace PostgresOutbox.Subscriptions.Replication;

public interface IReplicationDataMapper
{
    Task<IEnvelope> ReadFromSnapshot(NpgsqlDataReader reader, CancellationToken ct);

    Task<IEnvelope> ReadFromReplication(InsertMessage insertMessage, CancellationToken ct);
}
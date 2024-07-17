using Blumchen.Subscriptions.ReplicationMessageHandlers;
using Npgsql;
using Npgsql.Replication.PgOutput.Messages;

namespace Blumchen.Subscriptions.Replication;

public interface IReplicationDataMapper
{
    Task<IEnvelope> ReadFromSnapshot(NpgsqlDataReader reader, CancellationToken ct);

    Task<IEnvelope> ReadFromReplication(InsertMessage insertMessage, CancellationToken ct);

    
}

using Blumchen.Subscriptions.ReplicationMessageHandlers;
using Npgsql;
using Npgsql.Replication.PgOutput.Messages;

namespace Blumchen.Subscriptions.Replication;
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

public interface IReplicationDataMapper
{
    Task<IEnvelope> ReadFromSnapshot(NpgsqlDataReader reader, CancellationToken ct);

    Task<IEnvelope> ReadFromReplication(InsertMessage insertMessage, CancellationToken ct);
}

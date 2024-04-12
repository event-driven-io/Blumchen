using Npgsql;
using Npgsql.Replication.PgOutput.Messages;

namespace PostgresOutbox.Subscriptions.Replication;

public interface IReplicationDataMapper
{
    Task<object> ReadFromSnapshot(NpgsqlDataReader reader, CancellationToken ct);

    Task<object> ReadFromReplication(InsertMessage insertMessage, CancellationToken ct);
}

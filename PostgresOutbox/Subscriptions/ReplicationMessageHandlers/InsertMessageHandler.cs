using System.Text.Json;
using Npgsql.Replication.PgOutput.Messages;
using PostgresOutbox.Subscriptions.Replication;

namespace PostgresOutbox.Subscriptions.ReplicationMessageHandlers;

public static class InsertMessageHandler
{
    public static async Task<IEnvelope> Handle(
        InsertMessage message,
        IReplicationDataMapper dataMapper,
        CancellationToken ct
    )
    {
        try
        {
            return new OkEnvelope(await dataMapper.ReadFromReplication(message, ct));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or InvalidOperationException or JsonException)
        {
            return new KoEnvelope(ex);
        }
    }
}


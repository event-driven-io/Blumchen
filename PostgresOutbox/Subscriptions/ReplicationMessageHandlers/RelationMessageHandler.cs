using System.Collections.Concurrent;
using Npgsql.Replication.PgOutput.Messages;

namespace PostgresOutbox.Subscriptions.ReplicationMessageHandlers;

public static class RelationCache
{
    public static ConcurrentDictionary<uint, RelationMessage> Relations = new();
}

public class RelationMessageHandler
{

    public static void Handle(RelationMessage message) =>
        RelationCache.Relations[message.RelationId] = message;
}

using System.Runtime.CompilerServices;
using Npgsql;
using PostgresOutbox.Database;
using PostgresOutbox.Serialization;

namespace PostgresOutbox.Subscriptions.SnapshotReader;

public static class SnapshotReader
{
    public static async IAsyncEnumerable<object> GetEventsFromSnapshot(
        this NpgsqlConnection connection,
        string snapshotName,
        string tableName,
        [EnumeratorCancellation] CancellationToken ct
        )
    {
        await foreach (var @event in connection.QueryTransactionSnapshot(snapshotName,tableName, MapToEvent, ct))
        {
            yield return @event;
        }
    }

    private static async Task<object> MapToEvent(NpgsqlDataReader reader, CancellationToken ct)
    {
        var eventTypeName = reader.GetString(1);
        var eventType = Reflection.GetType.ByName(eventTypeName);

        var @event = await JsonSerialization.FromJsonAsync(eventType, await reader.GetStreamAsync(2, ct), ct);

        return @event!;
    }
}

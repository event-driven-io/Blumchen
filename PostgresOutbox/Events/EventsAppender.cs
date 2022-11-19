using Npgsql;
using PostgresOutbox.Serialization;

namespace PostgresOutbox.Events;

public static class EventsAppender
{
    public static async Task AppendAsync<T>(string tableName, T @event, string connectionString, CancellationToken ct)
        where T: class
    {
        var eventTypeName = typeof(T).AssemblyQualifiedName;
        var eventData = JsonSerialization.ToJson(@event);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        var command = connection.CreateCommand();
        command.CommandText = $"INSERT INTO {tableName}(event_type, data) values ('{eventTypeName}', '{eventData}')";
        await command.ExecuteNonQueryAsync(ct);
    }
}

using System.Collections;
using Npgsql;
using PostgresOutbox.Serialization;

namespace PostgresOutbox.Table;

public static class EventsAppender
{
    public static async Task AppendAsync<T>(string tableName, T @event, ITypeResolver resolver, string connectionString, CancellationToken ct)
        where T: class
    {
        var type = typeof(T);
        var (eventTypeName, jsonTypeInfo) = resolver.Resolve(type);
        var eventData = JsonSerialization.ToJson(@event, jsonTypeInfo);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        var command = connection.CreateCommand();
        command.CommandText = $"INSERT INTO {tableName}(event_type, data) values ('{eventTypeName}', '{eventData}')";
        await command.ExecuteNonQueryAsync(ct);
    }

    public static async Task AppendAsync<T>(string tableName, T @input, ITypeResolver resolver, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken ct)
        where T : class
    {
        switch (@input)
        {
            case null:
                throw new ArgumentNullException(nameof(@input));
            case IEnumerable @events:
                await AppendBatchAsyncOfT(tableName, events, resolver, connection, transaction, ct);
                break;
            default:
                await AppendAsyncOfT(tableName, input, resolver, connection, transaction, ct);
                break;
        }
    }

    private static async Task AppendBatchAsyncOfT<T>(
        string tableName
        , T events
        , ITypeResolver resolver
        , NpgsqlConnection connection
        , NpgsqlTransaction transaction
        , CancellationToken ct) where T : class, IEnumerable
    {
            var batch = new NpgsqlBatch(connection, transaction);
            foreach (var @event in @events)
            {
                var (eventTypeName, jsonTypeInfo) = resolver.Resolve(@event.GetType());
                var batchCommand = batch.CreateBatchCommand();
                var eventData = JsonSerialization.ToJson(@event, jsonTypeInfo);

            
                batchCommand.CommandText =
                    $"INSERT INTO {tableName}(event_type, data) values ('{eventTypeName}', '{eventData}')";
                batch.BatchCommands.Add(batchCommand);
            }
            await batch.ExecuteNonQueryAsync(ct);
    }

    private static async Task AppendAsyncOfT<T>(
        string tableName
        , T @event
        , ITypeResolver resolver
        , NpgsqlConnection connection
        , NpgsqlTransaction transaction
        , CancellationToken ct) where T : class
    {

        var (eventTypeName, jsonTypeInfo) = resolver.Resolve(typeof(T));
        var eventData = JsonSerialization.ToJson(@event, jsonTypeInfo);
        var command = new NpgsqlCommand(
            $"INSERT INTO {tableName}(event_type, data) values ('{eventTypeName}', '{eventData}')",
            connection,
            transaction
            );
        await command.ExecuteNonQueryAsync(ct);
    }
}

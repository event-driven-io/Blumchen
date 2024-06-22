using System.Collections;
using Blumchen.Serialization;
using Npgsql;

namespace Blumchen.Table;
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

public static class MessageAppender
{
    public static async Task AppendAsync<T>(string tableName, T @event, ITypeResolver resolver, string connectionString, CancellationToken ct)
        where T: class
    {
        var type = typeof(T);
        var (typeName, jsonTypeInfo) = resolver.Resolve(type);
        var data = JsonSerialization.ToJson(@event, jsonTypeInfo);

        var connection = new NpgsqlConnection(connectionString);
        await using var connection1 = connection.ConfigureAwait(false);
        await connection.OpenAsync(ct).ConfigureAwait(false);
        var command = connection.CreateCommand();
        command.CommandText = $"INSERT INTO {tableName}(message_type, data) values ('{typeName}', '{data}')";
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public static async Task AppendAsync<T>(string tableName, T @input, ITypeResolver resolver, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken ct)
        where T : class
    {
        switch (@input)
        {
            case null:
                throw new ArgumentNullException(nameof(@input));
            case IEnumerable inputs:
                await AppendBatchAsyncOfT(tableName, inputs, resolver, connection, transaction, ct).ConfigureAwait(false);
                break;
            default:
                await AppendAsyncOfT(tableName, input, resolver, connection, transaction, ct).ConfigureAwait(false);
                break;
        }
    }

    private static async Task AppendBatchAsyncOfT<T>(
        string tableName
        , T inputs
        , ITypeResolver resolver
        , NpgsqlConnection connection
        , NpgsqlTransaction transaction
        , CancellationToken ct) where T : class, IEnumerable
    {
            var batch = new NpgsqlBatch(connection, transaction);
            foreach (var input in inputs)
            {
                var (typeName, jsonTypeInfo) = resolver.Resolve(input.GetType());
                var batchCommand = batch.CreateBatchCommand();
                var data = JsonSerialization.ToJson(input, jsonTypeInfo);


                batchCommand.CommandText =
                    $"INSERT INTO {tableName}(message_type, data) values ('{typeName}', '{data}')";
                batch.BatchCommands.Add(batchCommand);
            }
            await batch.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static async Task AppendAsyncOfT<T>(
        string tableName
        , T @input
        , ITypeResolver resolver
        , NpgsqlConnection connection
        , NpgsqlTransaction transaction
        , CancellationToken ct) where T : class
    {

        var (typeName, jsonTypeInfo) = resolver.Resolve(typeof(T));
        var data = JsonSerialization.ToJson(@input, jsonTypeInfo);
        var command = new NpgsqlCommand(
            $"INSERT INTO {tableName}(message_type, data) values ('{typeName}', '{data}')",
            connection,
            transaction
            );
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}

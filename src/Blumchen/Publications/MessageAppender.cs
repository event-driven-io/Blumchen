using System.Collections;
using Blumchen.Serialization;
using Npgsql;

namespace Blumchen.Publications;   
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

public static class MessageAppender
{
    public static async Task AppendAsync<T>(T @input
        , (TableDescriptorBuilder.MessageTable tableDescriptor, IJsonTypeResolver jsonTypeResolver) resolver
        , NpgsqlConnection connection
        , NpgsqlTransaction transaction
        , CancellationToken ct
        ) where T : class
    {
        switch (@input)
        {
            case null:
                throw new ArgumentNullException(nameof(@input));
            case IEnumerable inputs:
                await AppendBatchAsyncOfT(inputs, resolver.tableDescriptor, resolver.jsonTypeResolver, connection, transaction, ct).ConfigureAwait(false);
                break;
            default:
                await AppendAsyncOfT(input, resolver.tableDescriptor, resolver.jsonTypeResolver, connection, transaction, ct).ConfigureAwait(false);
                break;
        }
    }

    private static async Task AppendAsyncOfT<T>(T input
        , TableDescriptorBuilder.MessageTable tableDescriptor
        , IJsonTypeResolver typeResolver
        , NpgsqlConnection connection
        , NpgsqlTransaction transaction
        , CancellationToken ct) where T : class
    {
        var (typeName, jsonTypeInfo) = typeResolver.Resolve(typeof(T));
        var data = JsonSerialization.ToJson(@input, jsonTypeInfo);
        var command = new NpgsqlCommand(
            $"INSERT INTO {tableDescriptor.Name}({tableDescriptor.MessageType.Name}, {tableDescriptor.Data.Name}) values ('{typeName}', '{data}')",
            connection,
            transaction
        );
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public static async Task AppendAsync<T>(T input
        , (TableDescriptorBuilder.MessageTable tableDescriptor, IJsonTypeResolver resolver) options
        , string connectionString
        , CancellationToken ct)
        where T: class
    {
        var type = typeof(T);
        var (typeName, jsonTypeInfo) = options.resolver.Resolve(type);
        var data = JsonSerialization.ToJson(input, jsonTypeInfo);

        var connection = new NpgsqlConnection(connectionString);
        await using var connection1 = connection.ConfigureAwait(false);
        await connection.OpenAsync(ct).ConfigureAwait(false);
        var command = connection.CreateCommand();
        command.CommandText =
            $"INSERT INTO {options.tableDescriptor.Name}({options.tableDescriptor.MessageType.Name}, {options.tableDescriptor.Data.Name}) values ('{typeName}', '{data}')";
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static async Task AppendBatchAsyncOfT<T>(T inputs
        , TableDescriptorBuilder.MessageTable tableDescriptor
        , IJsonTypeResolver resolver
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
                    $"INSERT INTO {tableDescriptor.Name}({tableDescriptor.MessageType.Name}, {tableDescriptor.Data.Name}) values ('{typeName}', '{data}')";
                batch.BatchCommands.Add(batchCommand);
            }
            await batch.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}

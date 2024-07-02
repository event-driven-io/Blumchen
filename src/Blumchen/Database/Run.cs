using System.Data;
using System.Runtime.CompilerServices;
using Blumchen.Subscriptions.Replication;
using Blumchen.Subscriptions.ReplicationMessageHandlers;
using Npgsql;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Blumchen.Database;

public static class Run
{
    private static async Task Execute(
        this NpgsqlDataSource dataSource,
        string sql,
        CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public static async Task EnsureTableExists(this NpgsqlDataSource dataSource, TableDescriptorBuilder.MessageTable tableDescriptor, CancellationToken ct)
        => await dataSource.Execute(tableDescriptor.ToString(), ct).ConfigureAwait(false);

    public static async Task<bool> Exists(
        this NpgsqlDataSource dataSource,
        string table,
        string where,
        object[] parameters,
        CancellationToken ct)
    {
        var command = dataSource.CreateCommand(
            $"SELECT EXISTS(SELECT 1 FROM {table} WHERE {where})"
        );
        await using var command1 = command.ConfigureAwait(false);
        foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter);

        return await command.ExecuteScalarAsync(ct).ConfigureAwait(false) as bool? == true;
    }

    internal static async IAsyncEnumerable<IEnvelope> QueryTransactionSnapshot(this NpgsqlConnection connection,
        string snapshotName,
        TableDescriptorBuilder.MessageTable tableDescriptor,
        ISet<string> registeredTypesKeys,
        IReplicationDataMapper dataMapper,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct).ConfigureAwait(false);
        await using var transaction1 = transaction.ConfigureAwait(false);

        var command =
            new NpgsqlCommand($"SET TRANSACTION SNAPSHOT '{snapshotName}';", connection, transaction);
        await using var command1 = command.ConfigureAwait(false);
        await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
        var whereClause = registeredTypesKeys.Count > 0
            ? $" WHERE {tableDescriptor.MessageType.Name} IN({PublicationFilter(registeredTypesKeys)})"
            : null;
        var cmd = new NpgsqlCommand($"SELECT * FROM {tableDescriptor.Name}{whereClause}", connection, transaction);
        await using var cmd1 = cmd.ConfigureAwait(false);
        var reader =  await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        await using var reader1 = reader.ConfigureAwait(false);

        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            yield return await dataMapper.ReadFromSnapshot(reader, ct).ConfigureAwait(false);
        yield break;

        static string PublicationFilter(ICollection<string> input) => string.Join(", ", input.Select(s => $"'{s}'"));
    }
}



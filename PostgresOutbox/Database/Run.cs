using System.Data;
using System.Runtime.CompilerServices;
using Npgsql;
using PostgresOutbox.Subscriptions.Replication;

namespace PostgresOutbox.Database;

public static class Run
{
    public static async Task Execute(
        this NpgsqlDataSource dataSource,
        string sql,
        CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync(ct);
    }

    public static async Task Execute(
        string connectionString,
        string sql,
        CancellationToken ct)
    {
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var command = dataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync(ct);
    }

    public static async Task<bool> Exists(
        this NpgsqlDataSource dataSource,
        string table,
        string where,
        object[] parameters,
        CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand(
            $"SELECT EXISTS(SELECT 1 FROM {table} WHERE {where})"
        );
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter);
        }

        return ((await command.ExecuteScalarAsync(ct)) as bool?) == true;
    }

    public static async IAsyncEnumerable<object> QueryTransactionSnapshot(
        this NpgsqlConnection connection,
        string snapshotName,
        string tableName,
        IReplicationDataMapper dataMapper,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);

        await using var command =
            new NpgsqlCommand($"SET TRANSACTION SNAPSHOT '{snapshotName}';", connection, transaction);
        await command.ExecuteScalarAsync(ct);

        await using var cmd = new NpgsqlCommand($"SELECT * FROM {tableName}", connection, transaction);
        await using var reader =  await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            yield return await dataMapper.ReadFromSnapshot(reader, ct);
        }
    }
}

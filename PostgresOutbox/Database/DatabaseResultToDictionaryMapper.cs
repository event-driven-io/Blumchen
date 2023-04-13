using Npgsql;
using Npgsql.Replication.PgOutput;

namespace PostgresOutbox.Database;

public static class DatabaseResultToDictionaryMapper
{
    public static async ValueTask<Dictionary<string, object>> ToDictionary(this ReplicationTuple tuple, CancellationToken ct)
    {
        var result = new Dictionary<string, object>();
        await foreach (var value in tuple)
        {
            var fieldName = value.GetPostgresType().Name;
            var fieldValue = await value.Get<object>(ct);
            result.Add(fieldName, fieldValue);
        }
        return result;
    }


    public static ValueTask<Dictionary<string, object>> ToDictionary(this NpgsqlDataReader reader, CancellationToken ct)
    {
        var result = new Dictionary<string, object>();

        for (var i = 0; i < reader.FieldCount; i++)
        {
            var value = reader.GetValue(i);
            result[reader.GetName(i)] = value;
        }

        return new ValueTask<Dictionary<string, object>>(result);
    }
}

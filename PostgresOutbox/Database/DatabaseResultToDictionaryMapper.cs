using Npgsql;
using Npgsql.Replication.PgOutput;
using Npgsql.Replication.PgOutput.Messages;

namespace PostgresOutbox.Database;

public static class DatabaseResultToDictionaryMapper
{
    public static async ValueTask<Dictionary<string, object>> ToDictionary(this InsertMessage message, CancellationToken ct)
    {
        var result = new Dictionary<string, object>();
        var columnIndex = 0;

        await foreach (var value in message.NewRow)
        {
            var fieldName = message.Relation.Columns[columnIndex].ColumnName;
            var fieldValue = await value.Get<object>(ct);
            result.Add(fieldName, fieldValue);

            columnIndex++;
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

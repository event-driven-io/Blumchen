using System.Globalization;
using PostgresOutbox.Reflection;

namespace PostgresOutbox.Subscriptions.Replication;

public class FlatObjectMapper<T>: DictionaryReplicationDataMapper<T> where T : notnull
{
    public FlatObjectMapper(Func<string, string>? transformName = null): base(Map(transformName))
    {
    }

    private static Func<IDictionary<string, object>, CancellationToken, ValueTask<T>> Map(
        Func<string, string>? transformName) =>
        (dictionary, _) => new ValueTask<T>(dictionary.Map<T>(transformName));
}

public static class NameTransformations
{
    public static string FromPostgres(string columnName) =>
        CultureInfo.InvariantCulture.TextInfo.ToTitleCase(columnName.Replace("_", "")).Replace(" ", "");
}

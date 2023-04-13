using PostgresOutbox.Reflection;

namespace PostgresOutbox.Subscriptions.Replication;

public class FlatObjectMapper<T>: DictionaryReplicationDataMapper<T> where T : notnull
{
    public FlatObjectMapper() : base(Map)
    {
    }

    private static ValueTask<T> Map(IDictionary<string, object> dictionary, CancellationToken ct) =>
        new(dictionary.Map<T>());
}

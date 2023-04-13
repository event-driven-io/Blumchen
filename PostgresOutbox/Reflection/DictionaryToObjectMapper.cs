using System.Dynamic;

namespace PostgresOutbox.Reflection;

public static class DictionaryToObjectMapper
{
    public static T Map<T>(this IDictionary<string, object> dict)
    {
        var expando = new ExpandoObject() as IDictionary<string, object>;
        foreach (var kvp in dict)
            expando.Add(kvp);

        dynamic dyn = expando;
        return dyn;
    }
}

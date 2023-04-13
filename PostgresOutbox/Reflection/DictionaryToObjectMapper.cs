namespace PostgresOutbox.Reflection;

public static class DictionaryToObjectMapper
{
    public static T Map<T>(this IDictionary<string, object> dict, Func<string, string>? transformName = null)
    {
        var properties = typeof(T).GetProperties();
        var obj = ObjectFactory<T>.GetDefaultOrUninitialized();

        foreach (var kvp in dict)
        {
            var property = properties.FirstOrDefault(p =>
                p.Name.Equals(transformName != null ? transformName(kvp.Key) : kvp.Key,
                    StringComparison.OrdinalIgnoreCase)
            );
            if (property == null) continue;

            var targetType =  Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;;

            var value = kvp.Value is IConvertible && kvp.GetType() != property.PropertyType
                ? Convert.ChangeType(kvp.Value, targetType)
                : kvp.Value;

            property.SetValue(obj, value);
        }

        return obj;
    }
}

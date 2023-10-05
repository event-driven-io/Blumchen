namespace PostgresOutbox.Reflection;

public class GetType
{
    private static readonly Dictionary<string, Type> Types = new();

    public static Type ByName(string typeName)
    {
        if (Types.ContainsKey(typeName))
            return Types[typeName];

        var type = GetFirstMatchingTypeFromCurrentDomainAssembly(typeName);

        if (type is null)
            throw new ArgumentOutOfRangeException(nameof(typeName));

        return Types[typeName] = type;
    }

    private static Type? GetFirstMatchingTypeFromCurrentDomainAssembly(string typeName)
    {
        return Type.GetType(typeName) ?? AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => new[] { a.GetType(typeName) }.Union(a.GetTypes().Where(x =>
                x.AssemblyQualifiedName == typeName || x.FullName == typeName || x.Name == typeName)))
            .FirstOrDefault();
    }
}

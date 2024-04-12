using System.Text.Json.Serialization.Metadata;

namespace PostgresOutbox.Subscriptions.Replication;

public interface ITypeResolver
{
    Type Resolve(string value);
    (string, JsonTypeInfo) Resolve(Type type);
}

public class FQNTypeResolver: ITypeResolver
{
    private readonly Dictionary<string, Type> _typeDictionary = new();
    private readonly Dictionary<Type, JsonTypeInfo> _typeInfoDictionary = new();

    public FQNTypeResolver WhiteList(Type supportedType, JsonTypeInfo typeInfo)
    {
        ArgumentException.ThrowIfNullOrEmpty(supportedType.AssemblyQualifiedName);
        _typeDictionary.Add(supportedType.AssemblyQualifiedName, supportedType);
        _typeInfoDictionary.Add(supportedType, typeInfo);
        return this;
    }

    public (string, JsonTypeInfo) Resolve(Type type) =>
        (_typeDictionary.Single(kv => kv.Value == type).Key, _typeInfoDictionary[type]);


    public Type Resolve(string type) => _typeDictionary[type];
}

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

    public FQNTypeResolver WhiteList(JsonTypeInfo typeInfo)
    {
        _typeDictionary.Add(typeInfo.Type.AssemblyQualifiedName!, typeInfo.Type);
        _typeInfoDictionary.Add(typeInfo.Type, typeInfo);
        return this;
    }

    public (string, JsonTypeInfo) Resolve(Type type) =>
        (_typeDictionary.Single(kv => kv.Value == type).Key, _typeInfoDictionary[type]);


    public Type Resolve(string type) => _typeDictionary[type];
}

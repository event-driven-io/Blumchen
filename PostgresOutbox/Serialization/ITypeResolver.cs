using System.Text.Json.Serialization.Metadata;

namespace PostgresOutbox.Serialization;

public interface ITypeResolver
{
    Type Resolve(string value);
    (string, JsonTypeInfo) Resolve(Type type);
}

public class TypeResolver(INamingPolicy? namingPolicy=default): ITypeResolver
{
    private static readonly Dictionary<string, Type> TypeDictionary = new();
    private static readonly Dictionary<Type, JsonTypeInfo> TypeInfoDictionary = new();

    public  TypeResolver WhiteList(JsonTypeInfo typeInfo)
    {
        TypeDictionary.Add((namingPolicy ?? new FQNNamingPolicy()).Bind(typeInfo.Type), typeInfo.Type);
        TypeInfoDictionary.Add(typeInfo.Type, typeInfo);
        return this;
    }

    public (string, JsonTypeInfo) Resolve(Type type) =>
        (TypeDictionary.Single(kv => kv.Value == type).Key, TypeInfoDictionary[type]);
    
    public Type Resolve(string type) => TypeDictionary[type];
}

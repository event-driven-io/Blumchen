using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace PostgresOutbox.Serialization;

public interface ITypeResolver
{
    Type Resolve(string value);
    (string, JsonTypeInfo) Resolve(Type type);
    JsonSerializerContext SerializationContext { get; }
}

public class TypeResolver(JsonSerializerContext serializationContext, INamingPolicy? namingPolicy=default): ITypeResolver
{
    public JsonSerializerContext SerializationContext { get; } = serializationContext;
    private static readonly Dictionary<string, Type> TypeDictionary = [];
    private static readonly Dictionary<Type, JsonTypeInfo> TypeInfoDictionary = [];

    public  TypeResolver WhiteList<T>() where T:class
    {
        var type = typeof(T);
        var typeInfo = SerializationContext.GetTypeInfo(type) ?? throw new NotSupportedException(type.FullName);
        TypeDictionary.TryAdd((namingPolicy ?? new FQNNamingPolicy()).Bind(typeInfo.Type), typeInfo.Type);
        TypeInfoDictionary.TryAdd(typeInfo.Type, typeInfo);
        return this;
    }

    public (string, JsonTypeInfo) Resolve(Type type) =>
        (TypeDictionary.Single(kv => kv.Value == type).Key, TypeInfoDictionary[type]);
    
    public Type Resolve(string type) => TypeDictionary[type];
}

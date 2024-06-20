using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Blumchen.Serialization;
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

public interface ITypeResolver
{
    Type Resolve(string value);
    (string, JsonTypeInfo) Resolve(Type type);
    JsonSerializerContext SerializationContext { get; }
}

public class TypeResolver(JsonSerializerContext serializationContext, INamingPolicy? namingPolicy=default): ITypeResolver
{
    public JsonSerializerContext SerializationContext { get; } = serializationContext;
    private static readonly ConcurrentDictionary<string, Type> TypeDictionary = [];
    private static readonly ConcurrentDictionary<Type, JsonTypeInfo> TypeInfoDictionary = [];

    public  TypeResolver WhiteList<T>() where T:class
    {
        var type = typeof(T);
        var typeInfo = SerializationContext.GetTypeInfo(type) ?? throw new NotSupportedException(type.FullName);
        TypeDictionary.AddOrUpdate((namingPolicy ?? new FQNNamingPolicy()).Bind(typeInfo.Type), _ => typeInfo.Type, (s,t) =>typeInfo.Type);
        TypeInfoDictionary.AddOrUpdate(typeInfo.Type, _ => typeInfo, (_,__)=> typeInfo);
        return this;
    }

    public (string, JsonTypeInfo) Resolve(Type type) =>
        (TypeDictionary.Single(kv => kv.Value == type).Key, TypeInfoDictionary[type]);

    public Type Resolve(string type) => TypeDictionary[type];
}

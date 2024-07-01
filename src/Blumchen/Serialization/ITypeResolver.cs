using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Blumchen.Serialization;
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

public interface ITypeResolver<T>
{
    (string, T) Resolve(Type type);
}

public interface IJsonTypeResolver: ITypeResolver<JsonTypeInfo>;

internal sealed class JsonTypeResolver(
    JsonSerializerContext serializationContext,
    INamingPolicy? namingPolicy = default)
    : IJsonTypeResolver
{
    public JsonSerializerContext SerializationContext { get; } = serializationContext;
    private readonly ConcurrentDictionary<string, Type> _typeDictionary = [];
    private readonly ConcurrentDictionary<Type, JsonTypeInfo> _typeInfoDictionary = [];
    private readonly INamingPolicy _namingPolicy = namingPolicy ?? new FQNNamingPolicy();

    internal void WhiteList(Type type)
    {
        var typeInfo = SerializationContext.GetTypeInfo(type) ?? throw new NotSupportedException(type.FullName);
        _typeDictionary.AddOrUpdate(_namingPolicy.Bind(typeInfo.Type), _ => typeInfo.Type, (s,t) =>typeInfo.Type);
        _typeInfoDictionary.AddOrUpdate(typeInfo.Type, _ => typeInfo, (_,__)=> typeInfo);
    }

    public (string, JsonTypeInfo) Resolve(Type type) =>
        (_typeDictionary.Single(kv => kv.Value == type).Key, _typeInfoDictionary[type]);

    internal IDictionary<string,Type> RegisteredTypes { get => _typeDictionary; }
    internal Type Resolve(string type) => _typeDictionary[type];
}


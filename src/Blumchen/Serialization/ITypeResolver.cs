using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Blumchen.Serialization;

public interface ITypeResolver<T>
{
    (string, T) Resolve(Type type);
    Type Resolve(string type);
    IDictionary<string, Type> RegisteredTypes { get; }
}

internal sealed class JsonTypeResolver(
    JsonSerializerContext serializationContext,
    INamingPolicy? namingPolicy = default)
    : ITypeResolver<JsonTypeInfo>
{
    public JsonSerializerContext SerializationContext { get; } = serializationContext;
    private readonly ConcurrentDictionary<string, Type> _typeDictionary = [];
    private readonly ConcurrentDictionary<Type, JsonTypeInfo> _typeInfoDictionary = [];
    private readonly INamingPolicy _namingPolicy = namingPolicy ?? new FQNNamingPolicy();

    internal void WhiteList(Type type)
    {
        var typeInfo = SerializationContext.GetTypeInfo(type) ?? throw new NotSupportedException(type.FullName);
        _typeDictionary.AddOrUpdate(_namingPolicy.Bind(typeInfo.Type), _ => typeInfo.Type, (_,_) =>typeInfo.Type);
        _typeInfoDictionary.AddOrUpdate(typeInfo.Type, _ => typeInfo, (_,_)=> typeInfo);
    }

    public (string, JsonTypeInfo) Resolve(Type type) =>
        (_typeDictionary.Single(kv => kv.Value == type).Key, _typeInfoDictionary[type]);

    public IDictionary<string,Type> RegisteredTypes { get => _typeDictionary; }
    public Type Resolve(string type) => _typeDictionary[type];
}


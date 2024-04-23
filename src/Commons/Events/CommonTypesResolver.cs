using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using PostgresOutbox.Serialization;

namespace Commons.Events;

public class CommonTypesResolver: ITypeResolver
{
    private readonly TypeResolver _inner = new TypeResolver(SourceGenerationContext.Default)
        .WhiteList<UserCreated>()
        .WhiteList<UserDeleted>();

    public Type Resolve(string value) => _inner.Resolve(value);
    public (string, JsonTypeInfo) Resolve(Type type) => _inner.Resolve(type);
    public JsonSerializerContext SerializationContext => _inner.SerializationContext;
}

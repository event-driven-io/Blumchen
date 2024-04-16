using System.Text.Json.Serialization.Metadata;
using PostgresOutbox.Subscriptions.Replication;

namespace Commons.Events;

public class CommonTypesResolver: ITypeResolver
{
    private readonly TypeResolver _inner = new TypeResolver()
        .WhiteList(SourceGenerationContext.Default.UserCreated )
        .WhiteList(SourceGenerationContext.Default.UserDeleted);

    public Type Resolve(string value) => _inner.Resolve(value);
    public (string, JsonTypeInfo) Resolve(Type type) => _inner.Resolve(type);
}


using System.Text.Json.Serialization.Metadata;
using PostgresOutbox.Subscriptions.Replication;

namespace Commons.Events;

public class TypeResolver: ITypeResolver
{
    private readonly FQNTypeResolver _inner = new FQNTypeResolver()
        .WhiteList(typeof(UserCreated), SourceGenerationContext.Default.UserCreated )
        .WhiteList(typeof(UserDeleted), SourceGenerationContext.Default.UserDeleted);

    public Type Resolve(string value) => _inner.Resolve(value);
    public (string, JsonTypeInfo) Resolve(Type type) => _inner.Resolve(type);
}


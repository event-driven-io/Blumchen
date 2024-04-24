using PostgresOutbox.Serialization;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Subscriber
{
    [MessageUrn("user-created:v1")]
    public record UserCreatedContract(
        Guid Id,
        string Name
    );

    [MessageUrn("user-deleted:v1")]
    public record UserDeletedContract(
        Guid Id,
        string Name
    );

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(UserCreatedContract))]
    [JsonSerializable(typeof(UserDeletedContract))]
    internal partial class SourceGenerationContext: JsonSerializerContext
    {
    }

    internal class SubscriberTypesResolver: ITypeResolver
    {
        private readonly TypeResolver _inner = new TypeResolver(SourceGenerationContext.Default, new AttributeNamingPolicy())
            .WhiteList<UserCreatedContract>()
            .WhiteList<UserDeletedContract>();

        public Type Resolve(string value) => _inner.Resolve(value);
        public (string, JsonTypeInfo) Resolve(Type type) => _inner.Resolve(type);
        public JsonSerializerContext SerializationContext => _inner.SerializationContext;
    }
}

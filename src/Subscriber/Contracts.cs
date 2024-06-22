using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Blumchen.Serialization;

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
        private static readonly TypeResolver Inner = new TypeResolver(SourceGenerationContext.Default, new AttributeNamingPolicy())
            .WhiteList<UserCreatedContract>()
            .WhiteList<UserDeletedContract>();

        public ISet<string> RegisteredTypes { get => Inner.RegisteredTypes; }
        public Type Resolve(string value) => Inner.Resolve(value);
        public (string, JsonTypeInfo) Resolve(Type type) => Inner.Resolve(type);
        public JsonSerializerContext SerializationContext => Inner.SerializationContext;
    }
}

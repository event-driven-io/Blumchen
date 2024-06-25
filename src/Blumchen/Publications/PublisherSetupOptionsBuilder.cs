using System.Text.Json.Serialization;
using Blumchen.Serialization;
using JetBrains.Annotations;

namespace Blumchen.Publications;

#pragma warning disable CS1591
public class PublisherSetupOptionsBuilder
{
    private INamingPolicy? _namingPolicy;
    private JsonSerializerContext? _jsonSerializerContext;

    [UsedImplicitly]
    public PublisherSetupOptionsBuilder NamingPolicy(INamingPolicy namingPolicy)
    {
        _namingPolicy = namingPolicy;
        return this;
    }

    [UsedImplicitly]
    public PublisherSetupOptionsBuilder JsonContext(JsonSerializerContext jsonSerializerContext)
    {
        _jsonSerializerContext = jsonSerializerContext;
        return this;
    }

    public IJsonTypeResolver Build()
    {
        ArgumentNullException.ThrowIfNull(_jsonSerializerContext);
        ArgumentNullException.ThrowIfNull(_namingPolicy);

        var jsonTypeResolver = new JsonTypeResolver(_jsonSerializerContext, _namingPolicy);
        using var typeEnum = _jsonSerializerContext.GetType()
            .GetCustomAttributesData()
            .Where(attributeData => attributeData.AttributeType == typeof(JsonSerializableAttribute))
            .Select(att => att.ConstructorArguments.Single())
            .Select(ca => ca.Value).OfType<Type>().GetEnumerator();
        while (typeEnum.MoveNext())
            jsonTypeResolver.WhiteList(typeEnum.Current);
        
        return jsonTypeResolver;
    }
}

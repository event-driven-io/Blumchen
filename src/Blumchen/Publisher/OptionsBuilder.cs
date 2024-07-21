using System.Text.Json.Serialization;
using Blumchen.Serialization;
using JetBrains.Annotations;
using static Blumchen.TableDescriptorBuilder;

namespace Blumchen.Publisher;

public class OptionsBuilder
{
    [System.Diagnostics.CodeAnalysis.NotNull]
    private INamingPolicy? _namingPolicy = default;

    [System.Diagnostics.CodeAnalysis.NotNull]
    private JsonSerializerContext? _jsonSerializerContext = default;

    private static readonly TableDescriptorBuilder TableDescriptorBuilder = new();

    private MessageTable? _tableDescriptor = default;

    [UsedImplicitly]
    public OptionsBuilder NamingPolicy(INamingPolicy namingPolicy)
    {
        _namingPolicy = namingPolicy;
        return this;
    }

    [UsedImplicitly]
    public OptionsBuilder JsonContext(JsonSerializerContext jsonSerializerContext)
    {
        _jsonSerializerContext = jsonSerializerContext;
        return this;
    }

    [UsedImplicitly]
    public OptionsBuilder WithTable(Func<TableDescriptorBuilder, TableDescriptorBuilder> builder)
    {
        _tableDescriptor = builder(TableDescriptorBuilder).Build();
        return this;
    }

    public PublisherOptions Build()
    {
        _tableDescriptor ??= TableDescriptorBuilder.Build();
        Ensure.NotNull(_jsonSerializerContext, nameof(JsonContext));
        Ensure.NotNull(_namingPolicy, nameof(NamingPolicy));

        var jsonTypeResolver = new JsonTypeResolver(_jsonSerializerContext, _namingPolicy);
        using var typeEnum = _jsonSerializerContext.GetType()
            .GetCustomAttributesData()
            .Where(attributeData => attributeData.AttributeType == typeof(JsonSerializableAttribute))
            .Select(att => att.ConstructorArguments.Single())
            .Select(ca => ca.Value).OfType<Type>().GetEnumerator();
        while (typeEnum.MoveNext())
            jsonTypeResolver.WhiteList(typeEnum.Current);
        
        return new(_tableDescriptor,jsonTypeResolver);
    }
}

using Blumchen.Serialization;

namespace Blumchen.Publications;

public record PublisherOptions(TableDescriptorBuilder.MessageTable TableDescriptor, IJsonTypeResolver JsonTypeResolver);

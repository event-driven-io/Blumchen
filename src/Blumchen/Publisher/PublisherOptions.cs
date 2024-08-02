using System.Text.Json.Serialization.Metadata;
using Blumchen.Database;
using Blumchen.Serialization;
using JetBrains.Annotations;
using Npgsql;

namespace Blumchen.Publisher;

public record PublisherOptions(TableDescriptorBuilder.MessageTable TableDescriptor, ITypeResolver<JsonTypeInfo> JsonTypeResolver);

public static class PublisherOptionsExtensions
{
    [UsedImplicitly]
    public static async Task<PublisherOptions> EnsureTable(this PublisherOptions publisherOptions, NpgsqlDataSource dataSource, CancellationToken ct)
    {
        await dataSource.EnsureTableExists(publisherOptions.TableDescriptor, ct);
        return publisherOptions;
    }

    [UsedImplicitly]
    public static async Task<PublisherOptions> EnsureTable(this PublisherOptions publisherOptions,
        string connectionString, CancellationToken ct)
        => await EnsureTable(publisherOptions, new NpgsqlDataSourceBuilder(connectionString).Build(), ct);
}

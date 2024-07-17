using System.Text.Json.Serialization.Metadata;
using Blumchen.Database;
using Blumchen.Serialization;
using Npgsql;

namespace Blumchen.Publications;

public record PublisherOptions(TableDescriptorBuilder.MessageTable TableDescriptor, ITypeResolver<JsonTypeInfo> JsonTypeResolver);

public static class PublisherOptionsExtensions
{
    public static async Task<PublisherOptions> EnsureTable(this PublisherOptions publisherOptions, NpgsqlDataSource dataSource, CancellationToken ct)
    {
        await dataSource.EnsureTableExists(publisherOptions.TableDescriptor, ct);
        return publisherOptions;
    }

    public static Task<PublisherOptions> EnsureTable(this PublisherOptions publisherOptions,
        string connectionString, CancellationToken ct)
        => EnsureTable(publisherOptions, new NpgsqlDataSourceBuilder(connectionString).Build(), ct);
}

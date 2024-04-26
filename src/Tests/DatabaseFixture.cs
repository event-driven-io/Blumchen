using System.Text.Json.Serialization.Metadata;
using Npgsql;
using PostgresOutbox.Database;
using PostgresOutbox.Serialization;
using PostgresOutbox.Subscriptions;
using PostgresOutbox.Subscriptions.Management;
using Testcontainers.PostgreSql;

namespace Tests;

public abstract class DatabaseFixture: IAsyncLifetime
{
    protected class TestConsumer<T>(Action<string> log, JsonTypeInfo info): IConsumes<T> where T : class
    {
        public async Task Handle(T value)
        {
            try
            {
                log(JsonSerialization.ToJson(value, info));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            await Task.CompletedTask;
        }
    }

    protected readonly PostgreSqlContainer Container = new PostgreSqlBuilder()
        .WithCommand("-c", "wal_level=logical")
        .Build();

    public Task InitializeAsync()
    {
        return Container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await Container.DisposeAsync();
    }

    protected static async Task<string> CreateEventsTable(
        NpgsqlDataSource dataSource,
        CancellationToken ct
    )
    {
        var tableName = Randomise("outbox");

        await dataSource.EnsureTableExists(tableName, ct);

        return tableName;
    }

    private static string Randomise(string prefix) =>
        $"{prefix}_{Guid.NewGuid().ToString().Replace("-", "")}";

    protected static (TypeResolver, TestConsumer<T>, ISubscriptionOptions) SetupFor<T>(
        string connectionString,
        string eventsTable,
        JsonTypeInfo info,
        Action<string> log,
        string? publicationName = null,
        string? slotName = null) where T : class
    {
        var typeResolver = new TypeResolver(SourceGenerationContext.Default).WhiteList<T>();
        var consumer = new TestConsumer<T>(log, info);
        var subscriptionOptions = new SubscriptionOptionsBuilder()
            .ConnectionString(connectionString)
            .TypeResolver(typeResolver)
            .Consumes<T, TestConsumer<T>>(consumer)
            .WithPublicationOptions(
                new PublicationManagement.PublicationSetupOptions(publicationName ?? Randomise("events_pub"), eventsTable)
            )
            .WithReplicationOptions(
                new ReplicationSlotManagement.ReplicationSlotSetupOptions(slotName ?? Randomise("events_slot"))
            ).Build();
        return (typeResolver, consumer, subscriptionOptions);
    }

}

using System.Text.Json.Serialization.Metadata;
using Commons.Events;
using Npgsql;
using PostgresOutbox.Serialization;
using PostgresOutbox.Subscriptions;
using PostgresOutbox.Subscriptions.Management;
using PostgresOutbox.Table;
using Testcontainers.PostgreSql;

namespace Tests;

public abstract class DatabaseFixture: IAsyncLifetime
{
    protected class TestConsumer<T>(Action<string> log, JsonTypeInfo info): IConsumes<T> where T : class
    {
        public T? Event { get; private set; }
        public async Task Handle(T value)
        {
            Event = value;
            log(JsonSerialization.ToJson(value, info));
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

        await EventTable.Ensure(dataSource, tableName, ct);

        return tableName;
    }

    protected static string Randomise(string prefix) =>
        $"{prefix}_{Guid.NewGuid().ToString().Replace("-", "")}";

    protected static (TypeResolver, TestConsumer<T>, ISubscriptionOptions) SetupFor<T>(
        string connectionString,
        string eventsTable,
        JsonTypeInfo info,
        Action<string> log
    ) where T : class
    {
        var typeResolver = new TypeResolver(SourceGenerationContext.Default).WhiteList<T>();
        var consumer = new TestConsumer<T>(log, info);
        var subscriptionOptions = new SubscriptionOptionsBuilder()
            .WithConnectionString(connectionString)
            .WithResolver(typeResolver)
            .Consumes<T, TestConsumer<T>>(consumer)
            .WithPublicationOptions(
                new PublicationManagement.PublicationSetupOptions(Randomise("events_pub"), eventsTable)
            )
            .WithReplicationOptions(
                new ReplicationSlotManagement.ReplicationSlotSetupOptions(Randomise("events_slot"))
            ).Build();
        return (typeResolver, consumer, subscriptionOptions);
    }

}

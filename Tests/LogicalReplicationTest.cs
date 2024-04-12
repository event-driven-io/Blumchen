using Commons.Events;
using PostgresOutbox.Events;
using PostgresOutbox.Serialization;
using PostgresOutbox.Subscriptions;
using PostgresOutbox.Subscriptions.Management;
using PostgresOutbox.Subscriptions.Replication;
using Testcontainers.PostgreSql;
using Xunit.Abstractions;

namespace Tests;
public class LogicalReplicationTest(ITestOutputHelper testOutputHelper) : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgreSqlContainer = new PostgreSqlBuilder()
        .WithCommand("-c", "wal_level=logical")
        .Build();

    public Task InitializeAsync()
    {
        return _postgreSqlContainer.StartAsync();
    }

    public Task DisposeAsync()
    {
        return _postgreSqlContainer.DisposeAsync().AsTask();
    }

    [Fact]
    public async Task WalSubscriptionForNewEventsShouldWork()
    {
        var cancellationTokenSource = new CancellationTokenSource();
        var ct = cancellationTokenSource.Token;

        var connectionString = _postgreSqlContainer.GetConnectionString();
        var eventsTable = await CreateEventsTable(connectionString, ct);

        var typeResolver = new FQNTypeResolver().WhiteList(typeof(UserCreated),SourceGenerationContext.Default.UserCreated);
        var subscriptionOptions = new SubscriptionOptionsBuilder()
            .WithConnectionString(connectionString)
            .WithMapper(
                new EventDataMapper(SourceGenerationContext.Default, typeResolver))
            .WitPublicationOptions(
                new PublicationManagement.PublicationSetupOptions(Randomise("events_pub"), eventsTable)
            )
            .WithReplicationOptions(
                new ReplicationSlotManagement.ReplicationSlotSetupOptions(Randomise("events_slot"))
            ).Build();
        
        var subscription = new Subscription();

        var events = subscription.Subscribe(subscriptionOptions, ct);

        var @event = new UserCreated(Guid.NewGuid(), Guid.NewGuid().ToString());
        await EventsAppender.AppendAsync(eventsTable, @event, typeResolver, connectionString, ct);

        await foreach (var readEvent in events)
        {
            testOutputHelper.WriteLine(JsonSerialization.ToJson(readEvent, SourceGenerationContext.Default.UserCreated));
            Assert.Equal(@event, readEvent);
            return;
        }
    }

    [Fact]
    public async Task WalSubscriptionForOldEventsShouldWork()
    {
        var cancellationTokenSource = new CancellationTokenSource();
        var ct = cancellationTokenSource.Token;

        var connectionString = _postgreSqlContainer.GetConnectionString();
        var eventsTable = await CreateEventsTable(connectionString, ct);
            
        var typeResolver = new FQNTypeResolver().WhiteList(typeof(UserCreated), SourceGenerationContext.Default.UserCreated);
        var subscriptionOptions = new SubscriptionOptionsBuilder()
            .WithConnectionString(connectionString)
            .WithMapper(
                new EventDataMapper(SourceGenerationContext.Default, typeResolver))
            .WitPublicationOptions(
                new PublicationManagement.PublicationSetupOptions(Randomise("events_pub"), eventsTable)
            )
            .WithReplicationOptions(
                new ReplicationSlotManagement.ReplicationSlotSetupOptions(Randomise("events_slot"))
            ).Build();
        var subscription = new Subscription();

        var @event = new UserCreated(Guid.NewGuid(), Guid.NewGuid().ToString());
        await EventsAppender.AppendAsync(eventsTable, @event, typeResolver, connectionString, ct);

        var events = subscription.Subscribe(subscriptionOptions, ct);

        await foreach (var readEvent in events)
        {
            testOutputHelper.WriteLine(JsonSerialization.ToJson(readEvent, SourceGenerationContext.Default.UserCreated));
            Assert.Equal(@event, readEvent);
            return;
        }
    }

    private static async Task<string> CreateEventsTable(
        string connectionString,
        CancellationToken ct
    )
    {
        var tableName = Randomise("outbox");

        await EventsTable.Create(connectionString, tableName, ct);

        return tableName;
    }

    private static string Randomise(string prefix) =>
        $"{prefix}_{Guid.NewGuid().ToString().Replace("-", "")}";
}

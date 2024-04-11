using Commons.Events;
using PostgresOutbox.Events;
using PostgresOutbox.Serialization;
using PostgresOutbox.Subscriptions;
using PostgresOutbox.Subscriptions.Management;
using PostgresOutbox.Subscriptions.Replication;
using Xunit.Abstractions;

namespace Tests;
//TODO: https://dotnet.testcontainers.org/
public class LogicalReplicationTest(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public async Task WalSubscriptionForNewEventsShouldWork()
    {
        var cancellationTokenSource = new CancellationTokenSource();
        var ct = cancellationTokenSource.Token;

        var eventsTable = await CreateEventsTable(ConnectionString, ct);

        var subscriptionOptions = new SubscriptionOptions(
            ConnectionString,
            new PublicationManagement.PublicationSetupOptions(Randomise("events_pub"),eventsTable),
            new ReplicationSlotManagement.ReplicationSlotSetupOptions(Randomise("events_slot")),
            new EventDataMapper()
        );
        var subscription = new Subscription();

        var events = subscription.Subscribe(subscriptionOptions, ct);

        var @event = new UserCreated(Guid.NewGuid(), Guid.NewGuid().ToString());
        await EventsAppender.AppendAsync(eventsTable, @event, ConnectionString, ct);

        await foreach (var readEvent in events.WithCancellation(ct))
        {
            testOutputHelper.WriteLine(JsonSerialization.ToJson(readEvent));
            Assert.Equal(@event, readEvent);
            return;
        }
    }

    [Fact]
    public async Task WalSubscriptionForOldEventsShouldWork()
    {
        var cancellationTokenSource = new CancellationTokenSource();
        var ct = cancellationTokenSource.Token;

        var eventsTable = await CreateEventsTable(ConnectionString, ct);

        var subscriptionOptions = new SubscriptionOptions(
            ConnectionString,
            new PublicationManagement.PublicationSetupOptions(Randomise("events_pub"),eventsTable),
            new ReplicationSlotManagement.ReplicationSlotSetupOptions(Randomise("events_slot")),
            new EventDataMapper()
        );
        var subscription = new Subscription();

        var @event = new UserCreated(Guid.NewGuid(), Guid.NewGuid().ToString());
        await EventsAppender.AppendAsync(eventsTable, @event, ConnectionString, ct);

        var events = subscription.Subscribe(subscriptionOptions, ct);

        await foreach (var readEvent in events)
        {
            testOutputHelper.WriteLine(JsonSerialization.ToJson(readEvent));
            Assert.Equal(@event, readEvent);
            return;
        }
    }

    private static async Task<string> CreateEventsTable(
        string connectionString,
        CancellationToken ct
    )
    {
        var tableName = Randomise("events");

        await EventsTable.Create(connectionString, tableName, ct);

        return tableName;
    }

    private static string Randomise(string prefix) =>
        $"{prefix}_{Guid.NewGuid().ToString().Replace("-", "")}";

    private const string ConnectionString =
        "PORT = 5432; HOST = localhost; TIMEOUT = 15; POOLING = True; MINPOOLSIZE = 1; MAXPOOLSIZE = 100; COMMANDTIMEOUT = 20; DATABASE = 'postgres'; PASSWORD = 'postgres'; USER ID = 'postgres'";
}

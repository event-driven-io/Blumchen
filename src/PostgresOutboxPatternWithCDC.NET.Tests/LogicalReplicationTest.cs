using Commons.Events;
using PostgresOutbox.Events;
using PostgresOutbox.Serialization;
using PostgresOutbox.Subscriptions;
using PostgresOutbox.Subscriptions.Replication;
using Xunit.Abstractions;
using static PostgresOutbox.Subscriptions.Management.PublicationManagement;
using static PostgresOutbox.Subscriptions.Management.ReplicationSlotManagement;

namespace PostgresOutboxPatternWithCDC.NET.Tests;

public class LogicalReplicationTest
{
    private readonly ITestOutputHelper testOutputHelper;

    public LogicalReplicationTest(ITestOutputHelper testOutputHelper)
    {
        this.testOutputHelper = testOutputHelper;
    }

    [Fact]
    public async Task WALSubscriptionForNewEventsShouldWork()
    {
        var cancellationTokenSource = new CancellationTokenSource();
        var ct = cancellationTokenSource.Token;

        var eventsTable = await CreateEventsTable(ConnectrionString, ct);

        var subscriptionOptions = new SubscriptionOptions(
            ConnectrionString,
            new PublicationSetupOptions(Randomise("events_pub"),eventsTable),
            new ReplicationSlotSetupOptions(Randomise("events_slot")),
            new EventDataMapper()
        );
        var subscription = new Subscription();

        var events = subscription.Subscribe(subscriptionOptions, ct);

        var @event = new UserCreated(Guid.NewGuid(), Guid.NewGuid().ToString());
        await EventsAppender.AppendAsync(eventsTable, @event, ConnectrionString, ct);

        await foreach (var readEvent in events.WithCancellation(ct))
        {
            testOutputHelper.WriteLine(JsonSerialization.ToJson(readEvent));
            Assert.Equal(@event, readEvent);
            return;
        }
    }

    [Fact]
    public async Task WALSubscriptionForOldEventsShouldWork()
    {
        var cancellationTokenSource = new CancellationTokenSource();
        var ct = cancellationTokenSource.Token;

        var eventsTable = await CreateEventsTable(ConnectrionString, ct);

        var subscriptionOptions = new SubscriptionOptions(
            ConnectrionString,
            new PublicationSetupOptions(Randomise("events_pub"),eventsTable),
            new ReplicationSlotSetupOptions(Randomise("events_slot")),
            new EventDataMapper()
        );
        var subscription = new Subscription();

        var @event = new UserCreated(Guid.NewGuid(), Guid.NewGuid().ToString());
        await EventsAppender.AppendAsync(eventsTable, @event, ConnectrionString, ct);

        var events = subscription.Subscribe(subscriptionOptions, ct);

        await foreach (var readEvent in events)
        {
            testOutputHelper.WriteLine(JsonSerialization.ToJson(readEvent));
            Assert.Equal(@event, readEvent);
            return;
        }
    }

    private async Task<string> CreateEventsTable(
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

    private const string ConnectrionString =
        "PORT = 5432; HOST = localhost; TIMEOUT = 15; POOLING = True; MINPOOLSIZE = 1; MAXPOOLSIZE = 100; COMMANDTIMEOUT = 20; DATABASE = 'postgres'; PASSWORD = 'Password12!'; USER ID = 'postgres'";
}

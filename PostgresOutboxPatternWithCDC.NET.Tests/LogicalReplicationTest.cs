using Npgsql;
using PostgresOutbox.Database;
using PostgresOutbox.Events;
using PostgresOutbox.Serialization;
using PostgresOutbox.Subscriptions;
using PostgresOutboxPatternWithCDC.NET.Tests.Events;
using Xunit.Abstractions;

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

        var subscriptionOptions = new EventsSubscriptionOptions(
            ConnectrionString,
            Randomise("events_slot"),
            Randomise("events_pub"),
            eventsTable
        );
        var subscription = new EventsSubscription();

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

        var subscriptionOptions = new EventsSubscriptionOptions(
            ConnectrionString,
            Randomise("events_slot"),
            Randomise("events_pub"),
            eventsTable
        );
        var subscription = new EventsSubscription();

        var @event = new UserCreated(Guid.NewGuid(), Guid.NewGuid().ToString());
        await EventsAppender.AppendAsync(eventsTable, @event, ConnectrionString, ct);

        var events = subscription.Subscribe(subscriptionOptions, ct);

        await foreach (var readEvent in events.WithCancellation(ct))
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

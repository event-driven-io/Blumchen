using PostgresOutbox.Events;
using PostgresOutbox.Serialization;
using PostgresOutbox.Subscriptions;
using Xunit.Abstractions;

namespace PostgresOutboxPatternWithCDC.NET.Tests;

record UserCreated(
    Guid Id,
    string Name
);

public class LogicalReplicationTest
{
    private readonly ITestOutputHelper testOutputHelper;

    public LogicalReplicationTest(ITestOutputHelper testOutputHelper)
    {
        this.testOutputHelper = testOutputHelper;
    }

    [Fact]
    public async Task WALSubscriptionShouldWork()
    {
        var cancellationTokenSource = new CancellationTokenSource();

        var ct = cancellationTokenSource.Token;

        var @event = new UserCreated(Guid.NewGuid(), Guid.NewGuid().ToString());
        await EventsAppender.AppendAsync(@event, ConnectrionString, ct);

        var subscriptionOptions = new EventsSubscriptionOptions(ConnectrionString, "events_slot", "events_pub");
        var subscription = new EventsSubscription(subscriptionOptions);

        await foreach (var readEvent in subscription.Subscribe(ct))
        {
            testOutputHelper.WriteLine(JsonSerialization.ToJson(readEvent));
            Assert.Equal(@event, readEvent);
            return;
        }
    }

    private const string ConnectrionString =
        "PORT = 5432; HOST = localhost; TIMEOUT = 15; POOLING = True; MINPOOLSIZE = 1; MAXPOOLSIZE = 100; COMMANDTIMEOUT = 20; DATABASE = 'postgres'; PASSWORD = 'Password12!'; USER ID = 'postgres'";
}

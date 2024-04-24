using Npgsql;
using PostgresOutbox.Subscriptions;
using PostgresOutbox.Table;
using Xunit.Abstractions;

namespace Tests;

public class WalSubscriptionForNewEvents(ITestOutputHelper testOutputHelper): DatabaseFixture
{
    [Fact]
    public async Task Execute()
    {
        var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var ct = cancellationTokenSource.Token;

        var connectionString = Container.GetConnectionString();
        var eventsTable = await CreateEventsTable(NpgsqlDataSource.Create(connectionString), ct);

        var (typeResolver, testConsumer, subscriptionOptions) = SetupFor<UserCreated>(connectionString, eventsTable, SourceGenerationContext.Default.UserCreated, testOutputHelper.WriteLine);
        await using var subscription = new Subscription();
        var events = subscription.Subscribe(_ => subscriptionOptions, ct);

        var @event = new UserCreated(Guid.NewGuid(), Guid.NewGuid().ToString());
        await EventsAppender.AppendAsync(eventsTable, @event, typeResolver, connectionString, ct);
        await foreach (var _ in events)
        {
            Assert.Equal(@event, testConsumer.Event);
            return;
        }
    }
}

using Commons.Events;
using Npgsql;
using PostgresOutbox.Subscriptions;
using PostgresOutbox.Table;
using Xunit.Abstractions;

namespace Tests;

public class WalSubscriptionForOldEvents(ITestOutputHelper testOutputHelper) : DatabaseFixture
{
    [Fact]
    public async Task Execute()
    {
        var cancellationTokenSource = new CancellationTokenSource();
        var ct = cancellationTokenSource.Token;
        var connectionString = Container.GetConnectionString();
        var eventsTable = await CreateEventsTable(NpgsqlDataSource.Create(connectionString), ct);
        var (typeResolver, testConsumer, subscriptionOptions) = SetupFor<UserDeleted>(connectionString, eventsTable, SourceGenerationContext.Default.UserDeleted, testOutputHelper.WriteLine);

        var @event = new UserDeleted(Guid.NewGuid(), Guid.NewGuid().ToString());
        await EventsAppender.AppendAsync(eventsTable, @event, typeResolver, connectionString, ct);

        await using var subscription = new Subscription();
        var events = subscription.Subscribe(_ => subscriptionOptions, ct);

        await foreach (var _ in events)
        {
            Assert.Equal(@event, testConsumer.Event);
            return;
        }
    }
}

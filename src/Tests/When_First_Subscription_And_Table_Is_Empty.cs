using Npgsql;
using PostgresOutbox.Subscriptions;
using PostgresOutbox.Subscriptions.ReplicationMessageHandlers;
using PostgresOutbox.Table;
using Xunit.Abstractions;

namespace Tests;

// ReSharper disable once InconsistentNaming
public class When_First_Subscription_And_Table_Is_Empty(ITestOutputHelper testOutputHelper): DatabaseFixture
{
    [Fact]
    public async Task Execute()
    {
        var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var ct = cancellationTokenSource.Token;

        var connectionString = Container.GetConnectionString();
        var eventsTable = await CreateOutboxTable(NpgsqlDataSource.Create(connectionString), ct);

        var (typeResolver, testConsumer, subscriptionOptions) = SetupFor<UserCreated>(connectionString, eventsTable,
            SourceGenerationContext.Default.UserCreated, testOutputHelper.WriteLine);
        await using var subscription = new Subscription();

        var @event = new UserCreated(Guid.NewGuid(), Guid.NewGuid().ToString());
        await MessageAppender.AppendAsync(eventsTable, @event, typeResolver, connectionString, ct);
        await foreach (var envelope in subscription.Subscribe(_ => subscriptionOptions, null, ct))
        {
            Assert.Equal(@event, ((OkEnvelope)envelope).Value);
            return;
        }
    }
}

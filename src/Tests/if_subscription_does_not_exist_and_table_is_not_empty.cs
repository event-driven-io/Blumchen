using Blumchen.Publisher;
using Blumchen.Serialization;
using Blumchen.Subscriptions;
using Blumchen.Subscriptions.Replication;
using Npgsql;
using Xunit.Abstractions;

namespace Tests;

// ReSharper disable once InconsistentNaming
public class if_subscription_does_not_exist_and_table_is_not_empty(ITestOutputHelper testOutputHelper): DatabaseFixture(testOutputHelper)
{
    [Fact]
    public async Task read_from_table_using_named_transaction_snapshot()
    {
        var ct = TimeoutTokenSource().Token;
        var sharedNamingPolicy = new AttributeNamingPolicy();
        var connectionString = Container.GetConnectionString();
        var eventsTable = await CreateOutboxTable(NpgsqlDataSource.Create(connectionString), ct);

        var resolver = new OptionsBuilder()
            .JsonContext(PublisherContext.Default)
            .NamingPolicy(sharedNamingPolicy)
            .WithTable(o => o.Name(eventsTable))
            .Build();

        //subscriber ignored msg
        await MessageAppender.AppendAsync( new PublisherUserDeleted(Guid.NewGuid(), Guid.NewGuid().ToString()), resolver, connectionString, ct);

        //poison message
        await InsertPoisoningMessage(connectionString, eventsTable, ct);

        var @event = new PublisherUserCreated(Guid.NewGuid(), Guid.NewGuid().ToString());
        await MessageAppender.AppendAsync(@event, resolver, connectionString, ct);

        var @expected = new SubscriberUserCreated(@event.Id, @event.Name);

        var ( _, subscriptionOptions) =
            SetupFor<SubscriberUserCreated>(connectionString, eventsTable, SubscriberContext.Default, sharedNamingPolicy, Output.WriteLine);
        var subscription = new Subscription();
        await using var subscription1 = subscription.ConfigureAwait(false);

        await foreach (var envelope in subscription.Subscribe(_ => subscriptionOptions, ct).ConfigureAwait(false))
        {
            Assert.Equal(@expected, ((OkEnvelope)envelope).Value);
            return;
        }
    }
}

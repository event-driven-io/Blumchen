using Blumchen.Publications;
using Blumchen.Serialization;
using Blumchen.Subscriptions;
using Blumchen.Subscriptions.ReplicationMessageHandlers;
using Npgsql;
using Xunit.Abstractions;

namespace Tests;

// ReSharper disable once InconsistentNaming
public class When_Subscription_Does_Not_Exist_And_Table_Is_Not_Empty(ITestOutputHelper testOutputHelper): DatabaseFixture(testOutputHelper)
{
    [Fact]
    public async Task Read_from_table_using_named_transaction_snapshot()
    {
        var ct = TimeoutTokenSource().Token;
        var sharedNamingPolicy = new AttributeNamingPolicy();
        var connectionString = Container.GetConnectionString();
        var eventsTable = await CreateOutboxTable(NpgsqlDataSource.Create(connectionString), ct);

        var resolver = new PublisherSetupOptionsBuilder()
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

        await foreach (var envelope in subscription.Subscribe(_ => subscriptionOptions, null, ct).ConfigureAwait(false))
        {
            Assert.Equal(@expected, ((OkEnvelope)envelope).Value);
            return;
        }
    }
}

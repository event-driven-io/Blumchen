using System.Diagnostics;
using Blumchen.Publications;
using Blumchen.Serialization;
using Blumchen.Subscriptions;
using Blumchen.Subscriptions.ReplicationMessageHandlers;
using Npgsql;
using Xunit.Abstractions;

namespace Tests;

// ReSharper disable once InconsistentNaming
public class When_First_Subscription_And_Table_Is_Empty(ITestOutputHelper testOutputHelper): DatabaseFixture
{
    [Fact]
    public async Task Read_from_table_using_named_transaction_snapshot()
    {
        var ct = TimeoutTokenSource().Token;

        var connectionString = Container.GetConnectionString();
        var eventsTable = await CreateOutboxTable(NpgsqlDataSource.Create(connectionString), ct);
        var sharedNamingPolicy = new AttributeNamingPolicy();
        var resolver = new PublisherSetupOptionsBuilder()
            .JsonContext(PublisherContext.Default)
            .NamingPolicy(sharedNamingPolicy)
            .Build();
        //poison msg
        await MessageAppender.AppendAsync(eventsTable, new PublisherUserDeleted(Guid.NewGuid(), Guid.NewGuid().ToString()), resolver, connectionString, ct);

        var @event = new PublisherUserCreated(Guid.NewGuid(), Guid.NewGuid().ToString());
        var @expected = new SubscriberUserCreated(@event.Id, @event.Name);

        await MessageAppender.AppendAsync(eventsTable, @event, resolver, connectionString, ct);

        var ( _, subscriptionOptions) = SetupFor<SubscriberUserCreated>(connectionString, eventsTable,
            SubscriberContext.Default, sharedNamingPolicy, testOutputHelper.WriteLine);
        var subscription = new Subscription();
        await using var subscription1 = subscription.ConfigureAwait(false);
        await foreach (var envelope in subscription.Subscribe(_ => subscriptionOptions, null, ct).ConfigureAwait(false))
        {
            Assert.Equal(@expected, ((OkEnvelope)envelope).Value);
            return;
        }
    }
}
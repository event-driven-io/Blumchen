using Blumchen.Publications;
using Blumchen.Serialization;
using Blumchen.Subscriptions;
using Blumchen.Subscriptions.ReplicationMessageHandlers;
using Npgsql;
using Xunit.Abstractions;

namespace Tests;

// ReSharper disable once InconsistentNaming
public class When_Subscription_Does_Not_Exist_And_Table_Is_Not_Empty(ITestOutputHelper testOutputHelper): DatabaseFixture
{
    [Fact]
    public async Task Execute()
    {
        var ct = TimeoutTokenSource().Token;
        var sharedNamingPolicy = new AttributeNamingPolicy();
        var connectionString = Container.GetConnectionString();
        var eventsTable = await CreateOutboxTable(NpgsqlDataSource.Create(connectionString), ct);

        var resolver = new PublisherSetupOptionsBuilder()
            .JsonContext(PublisherContext.Default)
            .NamingPolicy(sharedNamingPolicy)
            .Build();
        {
            var @event1 = new PublisherUserDeleted(Guid.NewGuid(), Guid.NewGuid().ToString());
            await MessageAppender.AppendAsync(eventsTable, @event1, resolver, connectionString, ct);
        }

        var @event = new PublisherUserCreated(Guid.NewGuid(), Guid.NewGuid().ToString());
        await MessageAppender.AppendAsync(eventsTable, @event, resolver, connectionString, ct);

        var @expected = new SubscriberUserCreated(@event.Id, @event.Name);

        var ( _, subscriptionOptions) =
            SetupFor<SubscriberUserCreated>(connectionString, eventsTable, SubscriberContext.Default, sharedNamingPolicy, testOutputHelper.WriteLine);
        var subscription = new Subscription();
        await using var subscription1 = subscription.ConfigureAwait(false);

        await foreach (var envelope in subscription.Subscribe(_ => subscriptionOptions, null, ct).ConfigureAwait(false))
        {
            Assert.Equal(@expected, ((OkEnvelope)envelope).Value);
            return;
        }
    }
}

using Blumchen.Publisher;
using Blumchen.Serialization;
using Blumchen.Subscriptions;
using Blumchen.Subscriptions.Management;
using Blumchen.Subscriptions.Replication;
using Npgsql;
using Xunit.Abstractions;

namespace Tests;

// ReSharper disable once InconsistentNaming
public class if_subscription_already_exists(ITestOutputHelper testOutputHelper): DatabaseFixture(testOutputHelper)
{
    [Fact]
    public async Task read_from_transaction_log()
    {
        var ct = TimeoutTokenSource().Token;
        var sharedNamingPolicy = new AttributeNamingPolicy();
        var connectionString = Container.GetConnectionString();
        var dataSource = NpgsqlDataSource.Create(connectionString);
        var eventsTable = await CreateOutboxTable(dataSource, ct);
        var opts = new OptionsBuilder()
            .JsonContext(PublisherContext.Default)
            .NamingPolicy(sharedNamingPolicy)
            .WithTable(o => o.Named(eventsTable))
            .Build();
        var slotName = "subscription_test";
        var publicationName = "publication_test";
        
        await dataSource.CreatePublication(publicationName, eventsTable, new HashSet<string>{"urn:message:user-created:v1"}, ct);
        
        var subscriptionOptions = SetupFor<SubscriberUserCreated>(connectionString, eventsTable,
            SubscriberContext.Default, sharedNamingPolicy, Output.WriteLine, publicationName: publicationName, slotName: slotName);

        //subscriber ignored msg
        await MessageAppender.AppendAsync(
            new PublisherUserDeleted(Guid.NewGuid(), Guid.NewGuid().ToString()), opts, connectionString, ct);

        //poison message
        await InsertPoisoningMessage(connectionString, eventsTable, ct);

        var @event = new PublisherUserCreated(Guid.NewGuid(), Guid.NewGuid().ToString());
        await MessageAppender.AppendAsync(@event, opts, connectionString, ct);

        var @expected = new SubscriberUserCreated(@event.Id, @event.Name);

        var subscription = new Subscription();
        await using var subscription1 = subscription.ConfigureAwait(false);
        await foreach (var envelope in subscription.Subscribe(_ => subscriptionOptions, ct).ConfigureAwait(false))
        {
            Assert.Equal(@expected, ((OkEnvelope)envelope).Value);
            return;
        }
    }
}

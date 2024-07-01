using System.Diagnostics;
using Blumchen.Database;
using Blumchen.Publications;
using Blumchen.Serialization;
using Blumchen.Subscriptions;
using Blumchen.Subscriptions.ReplicationMessageHandlers;
using Npgsql;
using Npgsql.Replication;
using Xunit.Abstractions;

namespace Tests;

// ReSharper disable once InconsistentNaming
public class When_Subscription_Already_Exists(ITestOutputHelper testOutputHelper): DatabaseFixture
{
    [Fact]
    public async Task Read_from_transaction_log()
    {
        var ct = TimeoutTokenSource().Token;
        var sharedNamingPolicy = new AttributeNamingPolicy();
        var connectionString = Container.GetConnectionString();
        var eventsTable = await CreateOutboxTable(NpgsqlDataSource.Create(connectionString), ct);
        var publisherResolver = new PublisherSetupOptionsBuilder()
            .JsonContext(PublisherContext.Default)
            .NamingPolicy(sharedNamingPolicy)
            .Build();
        var slotName = "subscription_test";
        var publicationName = "publication_test";
        await SetupReplication(connectionString, slotName, publicationName, eventsTable, ct);


        var ( _, subscriptionOptions) = SetupFor<SubscriberUserCreated>(connectionString, eventsTable,
            SubscriberContext.Default, sharedNamingPolicy, testOutputHelper.WriteLine, publicationName: publicationName, slotName: slotName);

        //subscriber ignored msg
        await MessageAppender.AppendAsync(eventsTable, new PublisherUserDeleted(Guid.NewGuid(), Guid.NewGuid().ToString()), publisherResolver, connectionString, ct);

        var @event = new PublisherUserCreated(Guid.NewGuid(), Guid.NewGuid().ToString());
        await MessageAppender.AppendAsync(eventsTable, @event, publisherResolver, connectionString, ct);

        var @expected = new SubscriberUserCreated(@event.Id, @event.Name);

        var subscription = new Subscription();
        await using var subscription1 = subscription.ConfigureAwait(false);
        await foreach (var envelope in subscription.Subscribe(_ => subscriptionOptions, null, ct).ConfigureAwait(false))
        {
            Assert.Equal(@expected, ((OkEnvelope)envelope).Value);
            return;
        }
    }

    private static async Task SetupReplication(
        string connectionString,
        string slotName,
        string publicationName,
        string tableName,
        CancellationToken ct)
    {
        var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var source = dataSource.ConfigureAwait(false);
        await dataSource.Execute($"CREATE PUBLICATION {publicationName} FOR TABLE {tableName} WITH (publish = 'insert');", ct).ConfigureAwait(false);
        var connection = new LogicalReplicationConnection(connectionString);
        await using var connection1 = connection.ConfigureAwait(false);
        await connection.Open(ct).ConfigureAwait(false);
        await connection.CreatePgOutputReplicationSlot(
            slotName,
            slotSnapshotInitMode: LogicalSlotSnapshotInitMode.Export,
            cancellationToken: ct
        ).ConfigureAwait(false);
    }
}

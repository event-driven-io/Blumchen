using Blumchen.Database;
using Blumchen.Subscriptions;
using Blumchen.Subscriptions.ReplicationMessageHandlers;
using Blumchen.Table;
using Npgsql;
using Npgsql.Replication;
using Xunit.Abstractions;

namespace Tests;

// ReSharper disable once InconsistentNaming
public class When_Subscription_Already_Exists(ITestOutputHelper testOutputHelper): DatabaseFixture
{
    [Fact]
    public async Task Execute()
    {
        var cancellationTokenSource = new CancellationTokenSource();
        var ct = cancellationTokenSource.Token;

        var connectionString = Container.GetConnectionString();
        var eventsTable = await CreateOutboxTable(NpgsqlDataSource.Create(connectionString), ct);
        var slotName = "subscription_test";
        var publicationName = "publication_test";
        await SetupReplication(connectionString, slotName, publicationName, eventsTable, ct);


        var (typeResolver, testConsumer, subscriptionOptions) = SetupFor<UserCreated>(connectionString, eventsTable,
            SourceGenerationContext.Default.UserCreated, testOutputHelper.WriteLine, publicationName, slotName);

        var @event = new UserCreated(Guid.NewGuid(), Guid.NewGuid().ToString());
        await MessageAppender.AppendAsync(eventsTable, @event, typeResolver, connectionString, ct);

        var subscription = new Subscription();
        await using var subscription1 = subscription.ConfigureAwait(false);
        await foreach (var envelope in subscription.Subscribe(_ => subscriptionOptions, null, ct).ConfigureAwait(false))
        {
            Assert.Equal(@event, ((OkEnvelope)envelope).Value);
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

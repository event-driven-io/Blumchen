using Npgsql;
using Npgsql.Replication;
using PostgresOutbox.Database;
using PostgresOutbox.Subscriptions;
using PostgresOutbox.Subscriptions.ReplicationMessageHandlers;
using PostgresOutbox.Table;
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

        await using var subscription = new Subscription();
        await foreach (var envelope in subscription.Subscribe(_ => subscriptionOptions, null, ct))
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
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await dataSource.Execute($"CREATE PUBLICATION {publicationName} FOR TABLE {tableName} WITH (publish = 'insert');", ct);
        await using var connection = new LogicalReplicationConnection(connectionString);
        await connection.Open(ct);
        await connection.CreatePgOutputReplicationSlot(
            slotName,
            slotSnapshotInitMode: LogicalSlotSnapshotInitMode.Export,
            cancellationToken: ct
        );
    }
}

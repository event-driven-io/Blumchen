using Blumchen.Serialization;
using Blumchen.Subscriptions;
using Blumchen.Subscriptions.ReplicationMessageHandlers;
using Blumchen.Table;
using Npgsql;
using Xunit.Abstractions;

namespace Tests;

// ReSharper disable once InconsistentNaming
public class When_Subscription_Does_Not_Exist_And_Table_Is_Not_Empty(ITestOutputHelper testOutputHelper) : DatabaseFixture
{
    [Fact]
    public async Task Execute()
    {
        var cancellationTokenSource = new CancellationTokenSource();
        var ct = cancellationTokenSource.Token;
        var connectionString = Container.GetConnectionString();
        var eventsTable = await CreateOutboxTable(NpgsqlDataSource.Create(connectionString), ct);


        var @event = new UserDeleted(Guid.NewGuid(), Guid.NewGuid().ToString());
        var typeResolver = new TypeResolver(SourceGenerationContext.Default).WhiteList<UserDeleted>();

        await MessageAppender.AppendAsync(eventsTable, @event, typeResolver, connectionString, ct);


        var (_, testConsumer, subscriptionOptions) =
            SetupFor<UserDeleted>(connectionString, eventsTable, SourceGenerationContext.Default.UserDeleted, testOutputHelper.WriteLine);
        await using var subscription = new Subscription();

        await foreach (var envelope in subscription.Subscribe(_ => subscriptionOptions, null, ct))
        {
            Assert.Equal(@event, ((OkEnvelope)envelope).Value);
            return;
        }
    }
}

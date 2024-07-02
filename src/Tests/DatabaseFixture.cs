using System.Diagnostics;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Blumchen;
using Blumchen.Database;
using Blumchen.Serialization;
using Blumchen.Subscriptions;
using Blumchen.Subscriptions.Management;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit.Abstractions;

namespace Tests;


public abstract class DatabaseFixture(ITestOutputHelper output): IAsyncLifetime
{
    protected ITestOutputHelper Output { get; } = output;
    protected readonly Func<CancellationTokenSource> TimeoutTokenSource = () => new(Debugger.IsAttached ?  TimeSpan.FromHours(1) : TimeSpan.FromSeconds(2));
    protected class TestConsumer<T>(Action<string> log, JsonTypeInfo info): IConsumes<T> where T : class
    {
        public async Task Handle(T value)
        {
            try
            {
                log(JsonSerialization.ToJson(value, info));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            await Task.CompletedTask.ConfigureAwait(false);
        }
    }

    protected readonly PostgreSqlContainer Container = new PostgreSqlBuilder()
        .WithCommand("-c", "wal_level=logical")
        .Build();

    public Task InitializeAsync() => Container.StartAsync();

    public async Task DisposeAsync() => await Container.DisposeAsync().ConfigureAwait(false);

    protected static async Task<string> CreateOutboxTable(
        NpgsqlDataSource dataSource,
        CancellationToken ct
    )
    {
        var tableName = Randomise("outbox");

        var tableDesc = new TableDescriptorBuilder().Name(tableName).Build();
        await dataSource.EnsureTableExists(tableDesc, ct).ConfigureAwait(false);

        return tableName;
    }

    private static string Randomise(string prefix) =>
        $"{prefix}_{Guid.NewGuid().ToString().Replace("-", "")}";

    protected static async Task InsertPoisoningMessage(string connectionString, string eventsTable, CancellationToken ct)
    {
        var connection = new NpgsqlConnection(connectionString);
        await using var connection1 = connection.ConfigureAwait(false);
        await connection.OpenAsync(ct);
        var command = connection.CreateCommand();
        command.CommandText = $"INSERT INTO {eventsTable}(message_type, data) values ('urn:message:user-created:v1', '{{\"prop\":\"some faking text\"}}')";
        await command.ExecuteNonQueryAsync(ct);
    }

    protected (TestConsumer<T> consumer, SubscriptionOptionsBuilder subscriptionOptionsBuilder) SetupFor<T>(
        string connectionString,
        string eventsTable,
        JsonSerializerContext info,
        INamingPolicy namingPolicy,
        Action<string> log,
        string? publicationName = null,
        string? slotName = null) where T : class
    {
        var jsonTypeInfo = info.GetTypeInfo(typeof(T));
        ArgumentNullException.ThrowIfNull(jsonTypeInfo);
        var consumer = new TestConsumer<T>(log, jsonTypeInfo);
        var subscriptionOptionsBuilder = new SubscriptionOptionsBuilder()
            .WithErrorProcessor(new TestOutErrorProcessor(Output))
            .ConnectionString(connectionString)
            .JsonContext(info)
            .NamingPolicy(namingPolicy)
            .Consumes<T, TestConsumer<T>>(consumer)
            .WithTable(o => o.Name(eventsTable))
            .WithPublicationOptions(
                new PublicationManagement.PublicationSetupOptions(PublicationName: publicationName ?? Randomise("events_pub"))
            )
            .WithReplicationOptions(
                new ReplicationSlotManagement.ReplicationSlotSetupOptions(slotName ?? Randomise("events_slot"))
            );
        return (consumer, subscriptionOptionsBuilder);
    }

    private sealed record TestOutErrorProcessor(ITestOutputHelper Output): IErrorProcessor
    {
        public Func<Exception, Task> Process => exception =>
        {
            Output.WriteLine($"record id:{0} resulted in error:{exception.Message}");
            return Task.CompletedTask;
        };
    }
}

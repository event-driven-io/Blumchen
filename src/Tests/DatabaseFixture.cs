using System.Diagnostics;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Blumchen;
using Blumchen.Database;
using Blumchen.Serialization;
using Blumchen.Subscriber;
using Blumchen.Subscriptions;
using Blumchen.Subscriptions.Management;
using Blumchen.Subscriptions.Replication;
using Microsoft.Extensions.Logging;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit.Abstractions;

namespace Tests;

public abstract class DatabaseFixture(ITestOutputHelper output): IAsyncLifetime
{
    protected ITestOutputHelper Output { get; } = output;
    protected readonly Func<CancellationTokenSource> TimeoutTokenSource = () => new(Debugger.IsAttached ?  TimeSpan.FromHours(1) : TimeSpan.FromSeconds(3));
    protected class TestMessageHandler<T>(Action<string> log, JsonTypeInfo info): IMessageHandler<T> where T : class
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

    protected class TestHandler<T>(ILogger<TestHandler<T>> logger): IMessageHandler<T> where T : class
    {
        public Task Handle(T value)
        {
            logger.LogTrace($"Message consumed:{value}");
            return Task.CompletedTask;
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

        var tableDesc = new TableDescriptorBuilder().Named(tableName).Build();
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

    protected  OptionsBuilder SetupFor<T>(
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
        var consumer = new TestMessageHandler<T>(log, jsonTypeInfo);
        var subscriptionOptionsBuilder = new OptionsBuilder()
            .WithErrorProcessor(new TestOutErrorProcessor(Output))
            .DataSource(new NpgsqlDataSourceBuilder(connectionString).Build())
            .ConnectionString(connectionString)

            .Consumes(consumer, opts => opts
                .WithJsonContext(info)
                .AndNamingPolicy(namingPolicy))

            .WithTable(o => o.Named(eventsTable))
            .WithPublicationOptions(
                new PublicationManagement.PublicationOptions(
                    PublicationName: publicationName ?? Randomise("events_pub"))
            )
            .WithReplicationOptions(
                new ReplicationSlotManagement.ReplicationSlotOptions(slotName ?? Randomise("events_slot"))
            );
        return subscriptionOptionsBuilder;
    }

    private sealed record TestOutErrorProcessor(ITestOutputHelper Output): IErrorProcessor
    {
        public Func<Exception, string, Task> Process => (exception, id) =>
        {
            Output.WriteLine($"record id:{id} resulted in error:{exception.Message}");
            return Task.CompletedTask;
        };
    }
}

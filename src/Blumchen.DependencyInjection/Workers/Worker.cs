using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using Blumchen.Serialization;
using Blumchen.Subscriptions;
using Blumchen.Subscriptions.Management;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Polly;


namespace Blumchen.Workers;

public abstract class Worker<T>(
    NpgsqlDataSource dataSource,
    string connectionString,
    IHandler<T> handler,
    JsonSerializerContext jsonSerializerContext,
    IErrorProcessor errorProcessor,
    ResiliencePipeline pipeline,
    INamingPolicy namingPolicy,
    PublicationManagement.PublicationSetupOptions publicationSetupOptions,
    ReplicationSlotManagement.ReplicationSlotSetupOptions replicationSlotSetupOptions,
    Func<TableDescriptorBuilder,TableDescriptorBuilder> tableDescriptorBuilder,
    ILogger logger): BackgroundService where T : class
{
    private string WorkerName { get; } = $"{nameof(Worker<T>)}<{typeof(T).Name}>";
    private static readonly ConcurrentDictionary<string, Action<ILogger, string, object[]>> LoggingActions = new(StringComparer.OrdinalIgnoreCase);
    private static void Notify(ILogger logger, LogLevel level, string template, params object[] parameters)
    {
        static Action<ILogger, string, object[]> LoggerAction(LogLevel ll, bool enabled) =>
            (ll, enabled) switch
            {
                (LogLevel.Information, true) => (logger, template, parameters) => logger.LogInformation(template, parameters),
                (LogLevel.Debug, true) => (logger, template, parameters) => logger.LogDebug(template, parameters),
                (_, _) => (_, _, _) => { }
            };
        LoggingActions.GetOrAdd(template,_ => LoggerAction(level, logger.IsEnabled(level)))(logger, template, parameters);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await pipeline.ExecuteAsync(async token =>
        {
            await using var subscription = new Subscription();
            await using var cursor = subscription.Subscribe(builder =>
                    builder
                        .DataSource(dataSource)
                        .ConnectionString(connectionString)
                        .WithTable(tableDescriptorBuilder)
                        .WithErrorProcessor(errorProcessor)
                        .Consumes<T, IHandler<T>>(handler)
                        .NamingPolicy(namingPolicy)
                        .JsonContext(jsonSerializerContext)
                        .WithPublicationOptions(publicationSetupOptions)
                        .WithReplicationOptions(replicationSlotSetupOptions)
                , ct: token).GetAsyncEnumerator(token);
            Notify(logger, LogLevel.Information,"{WorkerName} started", WorkerName);
            while (await cursor.MoveNextAsync().ConfigureAwait(false) && !token.IsCancellationRequested)
                Notify(logger, LogLevel.Debug, "{cursor.Current} processed", cursor.Current);

        }, stoppingToken).ConfigureAwait(false);
        Notify(logger, LogLevel.Information, "{WorkerName} stopped", WorkerName);
        return;
    }

}

using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using Blumchen.Configuration;
using Blumchen.Serialization;
using Blumchen.Subscriptions;
using Blumchen.Subscriptions.Management;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;


namespace Blumchen.Workers;

public abstract class Worker<T>(
    DatabaseOptions databaseOptions,
    IHandler<T> handler,
    JsonSerializerContext jsonSerializerContext,
    IErrorProcessor errorProcessor,
    ResiliencePipeline pipeline,
    INamingPolicy namingPolicy,
    PublicationManagement.PublicationSetupOptions publicationSetupOptions,
    ReplicationSlotManagement.ReplicationSlotSetupOptions replicationSlotSetupOptions,
    Func<TableDescriptorBuilder,TableDescriptorBuilder> tableDescriptorBuilder,
    ILoggerFactory loggerFactory): BackgroundService where T : class
{
    private readonly ILogger<Worker<T>> _logger = loggerFactory.CreateLogger<Worker<T>>();
    private string WorkerName { get; } = $"{nameof(Worker<T>)}<{typeof(T).Name}>";
    private static readonly ConcurrentDictionary<string, Action<ILogger, string, object[]>> _actions = new(StringComparer.OrdinalIgnoreCase);
    private static void Notify(ILogger logger, LogLevel level, string template, params object[] parameters)
    {
        static Action<ILogger, string, object[]> LoggerAction(LogLevel ll, bool enabled) =>
            (ll, enabled) switch
            {
                (LogLevel.Information, true) => (logger, template, parameters) => logger.LogInformation(template, parameters),
                (LogLevel.Debug, true) => (logger, template, parameters) => logger.LogDebug(template, parameters),
                (_, _) => (_, __, ___) => { }
            };
        _actions.GetOrAdd(template,s => LoggerAction(level, logger.IsEnabled(level)))(logger, template, parameters);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await pipeline.ExecuteAsync(async token =>
        {
            await using var subscription = new Subscription();
            await using var cursor = subscription.Subscribe(builder =>
                    builder
                        .ConnectionString(databaseOptions.ConnectionString)
                        .WithTable(tableDescriptorBuilder)
                        .WithErrorProcessor(errorProcessor)
                        .Handles<T, IHandler<T>>(handler)
                        .NamingPolicy(namingPolicy)
                        .JsonContext(jsonSerializerContext)
                        .WithPublicationOptions(publicationSetupOptions)
                        .WithReplicationOptions(replicationSlotSetupOptions)
                , ct: token, loggerFactory: loggerFactory).GetAsyncEnumerator(token);
            Notify(_logger, LogLevel.Information,"{WorkerName} started", WorkerName);
            while (await cursor.MoveNextAsync().ConfigureAwait(false) && !token.IsCancellationRequested)
                Notify(_logger, LogLevel.Debug, "{cursor.Current} processed", cursor.Current);

        }, stoppingToken).ConfigureAwait(false);
        Notify(_logger, LogLevel.Information, "{WorkerName} stopped", WorkerName);
        return;
    }

}

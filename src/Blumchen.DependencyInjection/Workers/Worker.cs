using System.Text.Json.Serialization;
using Blumchen.Serialization;
using Blumchen.Subscriptions;
using Blumchen.Subscriptions.Management;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;


namespace Blumchen.Workers;

public abstract class Worker<T>(
    DbOptions dbOptions,
    IHandler<T> handler,
    JsonSerializerContext jsonSerializerContext,
    IErrorProcessor errorProcessor,
    ResiliencePipeline pipeline,
    INamingPolicy namingPolicy,
    PublicationManagement.PublicationSetupOptions publicationSetupOptions,
    ReplicationSlotManagement.ReplicationSlotSetupOptions replicationSlotSetupOptions,
    ILoggerFactory loggerFactory): BackgroundService where T : class
{
    private readonly ILogger<Worker<T>> _logger = loggerFactory.CreateLogger<Worker<T>>();
    private string WorkerName => GetType().Name;


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        void Notify(LogLevel level, string template, params object[] parameters)
        {
            Action fn =(level, _logger.IsEnabled(level)) switch
            {
                (LogLevel.Information, true) => ()=> _logger.LogInformation(template,parameters),
                (LogLevel.Debug, true) => ()=> _logger.LogDebug(template, parameters),
                (_,_) => () =>{}
            };
            fn();
        }
        await pipeline.ExecuteAsync(async token =>
        {
            await using var subscription = new Subscription();
            await using var cursor = subscription.Subscribe(builder =>
                    builder
                        .ConnectionString(dbOptions.ConnectionString)
                        .WithErrorProcessor(errorProcessor)
                        .Handles<T, IHandler<T>>(handler)
                        .NamingPolicy(namingPolicy)
                        .JsonContext(jsonSerializerContext)
                        .WithPublicationOptions(publicationSetupOptions)
                        .WithReplicationOptions(replicationSlotSetupOptions)
                , ct: token, loggerFactory: loggerFactory).GetAsyncEnumerator(token);
            Notify(LogLevel.Information,"{WorkerName} started", WorkerName);
            while (await cursor.MoveNextAsync().ConfigureAwait(false) && !token.IsCancellationRequested)
                Notify(LogLevel.Debug, "{cursor.Current} processed", cursor.Current);

        }, stoppingToken).ConfigureAwait(false);
        Notify(LogLevel.Information, "{WorkerName} started", WorkerName);
    }

}



public record DbOptions(string ConnectionString);


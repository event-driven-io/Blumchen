using Blumchen.Subscriptions;
using Blumchen.Subscriptions.Replication;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Blumchen.DependencyInjection;

public class Worker<T>(
    WorkerOptions options,
    ILogger<Worker<T>> logger): BackgroundService where T : class, IMessageHandler
{
    private string WorkerName { get; } = $"{nameof(Worker<T>)}<{typeof(T).Name}>";
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await options.OuterPipeline.ExecuteAsync(async token =>
            await options.InnerPipeline.ExecuteAsync(async ct =>
        {
            await using var subscription = new Subscription();
            await using var cursor = subscription.Subscribe(options.SubscriberOptions, ct)
                .GetAsyncEnumerator(ct);
            logger.ServiceStarted(WorkerName);
            while (await cursor.MoveNextAsync().ConfigureAwait(false) && !ct.IsCancellationRequested)
               logger.MessageProcessed(cursor.Current);
        }, token).ConfigureAwait(false), stoppingToken).ConfigureAwait(false);
        logger.ServiceStopped(WorkerName);
    }

}

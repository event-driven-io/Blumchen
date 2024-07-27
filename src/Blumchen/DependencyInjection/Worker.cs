using System.Collections.Concurrent;
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
    private static readonly ConcurrentDictionary<string, Action<ILogger, string, object[]>> LoggingActions = new(StringComparer.OrdinalIgnoreCase);
    private static void Notify(ILogger logger, LogLevel level, string template, params object[] parameters)
    {
        LoggingActions.GetOrAdd(template,_ => LoggerAction(level, logger.IsEnabled(level)))(logger, template, parameters);
        return;

        static Action<ILogger, string, object[]> LoggerAction(LogLevel ll, bool enabled) =>
            (ll, enabled) switch
            {
                (LogLevel.Information, true) => (logger, template, parameters) => logger.LogInformation(template, parameters),
                (LogLevel.Debug, true) => (logger, template, parameters) => logger.LogDebug(template, parameters),
                (LogLevel.Trace, true) => (logger, template, parameters) => logger.LogTrace(template, parameters),
                (LogLevel.Warning, true) => (logger, template, parameters) => logger.LogWarning(template, parameters),
                (LogLevel.Error, true) => (logger, template, parameters) => logger.LogError(template, parameters),
                (LogLevel.Critical, true) => (logger, template, parameters) => logger.LogCritical(template, parameters),
                (_, _) => (_, _, _) => { }
            };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await options.ResiliencePipeline.ExecuteAsync(async token =>
        {
            await using var subscription = new Subscription();
            await using var cursor = subscription.Subscribe(options.SubscriberOptions, ct: token)
                .GetAsyncEnumerator(token);
            Notify(logger, LogLevel.Information,"{WorkerName} started", WorkerName);
            while (await cursor.MoveNextAsync().ConfigureAwait(false) && !token.IsCancellationRequested)
                Notify(logger, LogLevel.Trace, "{cursor.Current} processed", cursor.Current);

        }, stoppingToken).ConfigureAwait(false);
        Notify(logger, LogLevel.Information, "{WorkerName} stopped", WorkerName);
    }

}

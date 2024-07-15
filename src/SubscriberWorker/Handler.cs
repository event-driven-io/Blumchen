using Blumchen.Subscriptions;
using Microsoft.Extensions.Logging;
#pragma warning disable CS9113 // Parameter is unread.

namespace SubscriberWorker;


public class Handler<T>(ILogger logger): IHandler<T> where T : class
{
    private Task ReportSuccess(int count)
    {
        if(logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug($"Read #{count} messages {typeof(T).FullName}");
        return Task.CompletedTask;
    }

    private int _counter;
    private int _completed;
    public Task Handle(T value)
        => Interlocked.Increment(ref _counter) % 10 == 0
            //Simulating some exception on out of process dependencies
            ? Task.FromException(new Exception($"Error on publishing {nameof(T)}"))
            : ReportSuccess(Interlocked.Increment(ref _completed));
}

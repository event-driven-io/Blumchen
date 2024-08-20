using Blumchen.Subscriptions.Replication;
using Microsoft.Extensions.Logging;
#pragma warning disable CS9113 // Parameter is unread.

namespace SubscriberWorker;

public class HandlerBase(ILogger<HandlerBase> logger)
{
    private int _counter;
    private int _completed;

    private Task ReportSuccess<T>(int count)
    {
        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug($"Read #{count} messages {typeof(T).FullName}");
        return Task.CompletedTask;
    }

    protected Task Handle<T>(T value) =>
        Interlocked.Increment(ref _counter) % 10 == 0
            //Simulating some exception on out of process dependencies
            ? Task.FromException(new Exception($"Error on publishing {typeof(T).FullName}"))
            : ReportSuccess<T>(Interlocked.Increment(ref _completed));
}

public class HandleImpl2(ILogger<HandleImpl2> logger)
    : HandlerBase(logger), IMessageHandler<UserDeletedContract>
{
    public Task Handle(UserDeletedContract value) => Handle<UserDeletedContract>(value);
}

public class HandleImpl1(ILogger<HandleImpl1> logger)
    : HandlerBase(logger), IMessageHandler<UserCreatedContract>, IMessageHandler<UserModifiedContract>
{
    public Task Handle(UserCreatedContract value) => Handle<UserCreatedContract>(value);

    public Task Handle(UserModifiedContract value) =>  Handle<UserModifiedContract>(value);
}

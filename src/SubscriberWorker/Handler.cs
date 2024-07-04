using Blumchen.Subscriptions;

namespace SubscriberWorker;


public class Handler<T>: IHandler<T> where T : class
{
    private volatile int _counter;
    public Task Handle(T value)
        => Interlocked.Increment(ref _counter) % 3 == 0
            ? Task.FromException(new Exception($"Error on publishing {nameof(T)}"))
            : Task.CompletedTask;
}

namespace Blumchen.Subscriptions;

internal class ObjectTracingConsumer: IHandler<object>
{
    private static ulong _counter = 0;
    public Task Handle(object value)
    {
        Interlocked.Increment(ref _counter);
        return Console.Out.WriteLineAsync();
    }
}

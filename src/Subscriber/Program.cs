using Commons;
using PostgresOutbox.Serialization;
using PostgresOutbox.Subscriptions;
using Subscriber;

#pragma warning disable CS8601 // Possible null reference assignment.
Console.Title = typeof(Program).Assembly.GetName().Name;
#pragma warning restore CS8601 // Possible null reference assignment.
var cancellationTokenSource = new CancellationTokenSource();


AppDomain.CurrentDomain.UnhandledException += (_, e) => Console.Out.WriteLine(e.ExceptionObject.ToString());
TaskScheduler.UnobservedTaskException += (_,e) => Console.Out.WriteLine(e.Exception.ToString());

var ct = cancellationTokenSource.Token;
var consumer = new Consumer();
await using var subscription = new Subscription();

try
{
    await using var cursor = subscription.Subscribe(
        builder => builder
            .WithConnectionString(Settings.ConnectionString)
            .WithResolver(new SubscriberTypesResolver())
            .Consumes<UserCreatedContract, Consumer>(consumer)
            .Consumes<UserDeletedContract, Consumer>(consumer)
            .Build(), ct
    ).GetAsyncEnumerator(ct);
    while (await cursor.MoveNextAsync() && !ct.IsCancellationRequested);
}
catch (Exception e)
{
    Console.WriteLine(e);
}

Console.ReadKey();

internal class Consumer:
    IConsumes<UserCreatedContract>,
    IConsumes<UserDeletedContract>
{
    public Task Handle(UserCreatedContract value) => Console.Out.WriteLineAsync(JsonSerialization.ToJson(value, SourceGenerationContext.Default.UserCreatedContract));
    public Task Handle(UserDeletedContract value) => Console.Out.WriteLineAsync(JsonSerialization.ToJson(value, SourceGenerationContext.Default.UserDeletedContract));
}

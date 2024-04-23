using Commons;
using Commons.Events;
using PostgresOutbox.Serialization;
using PostgresOutbox.Subscriptions;

#pragma warning disable CS8601 // Possible null reference assignment.
Console.Title = typeof(Program).Assembly.GetName().Name;
#pragma warning restore CS8601 // Possible null reference assignment.
var cancellationTokenSource = new CancellationTokenSource();

var ct = cancellationTokenSource.Token;
var consumer = new Consumer();
await using var subscription = new Subscription();
await using var cursor = subscription.Subscribe(
    builder => builder
        .WithConnectionString(Settings.ConnectionString)
        .WithResolver(new CommonTypesResolver())
        .Consumes<UserCreated, Consumer>(consumer)
        .Consumes<UserDeleted, Consumer>(consumer)
        .Build(), ct
).GetAsyncEnumerator(ct);
while (await cursor.MoveNextAsync() && !ct.IsCancellationRequested);


internal class Consumer:
    IConsumes<UserCreated>,
    IConsumes<UserDeleted>
{
    public Task Handle(UserCreated value) => Console.Out.WriteLineAsync(JsonSerialization.ToJson(value, SourceGenerationContext.Default.UserCreated));
    public Task Handle(UserDeleted value) => Console.Out.WriteLineAsync(JsonSerialization.ToJson(value, SourceGenerationContext.Default.UserDeleted));
}

using Blumchen.Serialization;
using Blumchen.Subscriptions;
using Commons;
using Microsoft.Extensions.Logging;
using Npgsql;
using Subscriber;

#pragma warning disable CS8601 // Possible null reference assignment.
Console.Title = typeof(Program).Assembly.GetName().Name;
#pragma warning restore CS8601 // Possible null reference assignment.
var cancellationTokenSource = new CancellationTokenSource();


AppDomain.CurrentDomain.UnhandledException += (_, e) => Console.Out.WriteLine(e.ExceptionObject.ToString());
TaskScheduler.UnobservedTaskException += (_,e) => Console.Out.WriteLine(e.Exception.ToString());

var ct = cancellationTokenSource.Token;
var consumer = new Consumer();
var subscription = new Subscription();
await using var subscription1 = subscription.ConfigureAwait(false);

try
{
    var dataSourceBuilder = new NpgsqlDataSourceBuilder(Settings.ConnectionString)
        .UseLoggerFactory(LoggerFactory.Create(builder => builder.AddConsole()));
    var cursor = subscription.Subscribe(
        builder => builder
            .DataSource(dataSourceBuilder.Build())
            .ConnectionString(Settings.ConnectionString)
            .WithTable(options => options
                .Id("id")
                .MessageType("message_type")
                .MessageData("data", new MimeType.Json())
            )
            .NamingPolicy(new AttributeNamingPolicy())
            .JsonContext(SourceGenerationContext.Default)
            .Consumes<UserCreatedContract>(consumer)
            .Consumes<UserDeletedContract>(consumer), ct:ct
    ).GetAsyncEnumerator(ct);
    await using var cursor1 = cursor.ConfigureAwait(false);
    while (await cursor.MoveNextAsync().ConfigureAwait(false) && !ct.IsCancellationRequested);
}
catch (Exception e)
{
    Console.WriteLine(e);
}

Console.ReadKey();

namespace Subscriber
{
    internal class Consumer:
        IMessageHandler<UserCreatedContract>,
        IMessageHandler<UserDeletedContract>
    {
        public Task Handle(UserCreatedContract value) => Console.Out.WriteLineAsync(JsonSerialization.ToJson(value, SourceGenerationContext.Default.UserCreatedContract));
        public Task Handle(UserDeletedContract value) => Console.Out.WriteLineAsync(JsonSerialization.ToJson(value, SourceGenerationContext.Default.UserDeletedContract));
    }
}

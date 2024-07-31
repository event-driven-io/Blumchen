using Blumchen.Serialization;
using Blumchen.Subscriptions;
using Blumchen.Subscriptions.Replication;
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
var subscription = new Subscription();
await using var subscription1 = subscription.ConfigureAwait(false);

try
{
    
    var loggerFactory = LoggerFactory.Create(builder => builder
        .AddFilter("Microsoft", LogLevel.Warning)
        .AddFilter("System", LogLevel.Warning)
        .AddFilter("Npgsql", LogLevel.Information)
        .AddFilter("Blumchen", LogLevel.Debug)
        .AddFilter("Subscriber", LogLevel.Trace)
        .AddSimpleConsole());
    var logger = loggerFactory.CreateLogger("Subscriber");
    var dataSourceBuilder = new NpgsqlDataSourceBuilder(Settings.ConnectionString)
        .UseLoggerFactory(loggerFactory);
    var cursor = subscription.Subscribe(
        builder =>
        {
            var consumer = new Consumer(loggerFactory.CreateLogger<Consumer>());
            return builder
                .DataSource(dataSourceBuilder.Build())
                .ConnectionString(Settings.ConnectionString)
                .WithTable(options => options
                    .Id("id")
                    .MessageType("message_type")
                    .MessageData("data")
                )
                .Consumes<UserCreatedContract>(consumer, opts =>
                    opts
                        .WithJsonContext(SourceGenerationContext.Default)
                        .AndNamingPolicy(new AttributeNamingPolicy()))
                .ConsumesRawString<MessageString>(consumer)
                .ConsumesRawObject<MessageObjects>(consumer);
        }
        //OR
        //.ConsumesRawStrings(consumer)
        //OR
        //.ConsumesRawObjects(consumer)
        , ct
    ).GetAsyncEnumerator(ct);
    await using var cursor1 = cursor.ConfigureAwait(false);
    while (await cursor.MoveNextAsync().ConfigureAwait(false) && !ct.IsCancellationRequested)
        if(logger.IsEnabled(LogLevel.Trace))
            logger.LogTrace($"{cursor.Current} processed");
}
catch (Exception e)
{
    Console.WriteLine(e);
}

Console.ReadKey();

namespace Subscriber
{
    internal class Consumer(ILogger<Consumer> logger):
        IMessageHandler<UserCreatedContract>,
        IMessageHandler<object>,
        IMessageHandler<string>
    {
        private int _completed;

        private Task ReportSuccess<T>(int count)
        {
            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug($"Read #{count} messages {typeof(T).FullName}");
            return Task.CompletedTask;
        }

        private Task Handle<T>(T value) =>
                ReportSuccess<T>(Interlocked.Increment(ref _completed));

        public Task Handle(string value) => Handle<string>(value);
        public Task Handle(object value) => Handle<object>(value);
        public Task Handle(UserCreatedContract value)  =>
            Handle<UserCreatedContract>(value);

    }
}

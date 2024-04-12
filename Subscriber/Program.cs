using Commons;
using Commons.Events;
using PostgresOutbox.Serialization;
using PostgresOutbox.Subscriptions;
using PostgresOutbox.Subscriptions.Replication;

#pragma warning disable CS8601 // Possible null reference assignment.
Console.Title = typeof(Program).Assembly.GetName().Name;
#pragma warning restore CS8601 // Possible null reference assignment.
var cancellationTokenSource = new CancellationTokenSource();

var ct = cancellationTokenSource.Token;


await foreach (var @event in new Subscription().Subscribe(
                   new SubscriptionOptionsBuilder()
                       .WithConnectionString(Settings.ConnectionString)
                       .WithMapper(new EventDataMapper(SourceGenerationContext.Default, new TypeResolver()))
                   .Build(), ct
               ))

    switch (@event)
    {
        case UserCreated c:
            Console.WriteLine(JsonSerialization.ToJson(c, SourceGenerationContext.Default.UserCreated));
            break;
        case UserDeleted d:
            Console.WriteLine(JsonSerialization.ToJson(d, SourceGenerationContext.Default.UserDeleted));
            break;
    }


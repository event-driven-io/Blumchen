using Commons;
using Commons.Events;
using PostgresOutbox.Serialization;
using PostgresOutbox.Subscriptions;

#pragma warning disable CS8601 // Possible null reference assignment.
Console.Title = typeof(Program).Assembly.GetName().Name;
#pragma warning restore CS8601 // Possible null reference assignment.
var cancellationTokenSource = new CancellationTokenSource();

var ct = cancellationTokenSource.Token;
var i = 1;
await foreach (var @event in new Subscription().Subscribe(
                   builder => builder
                       .WithConnectionString(Settings.ConnectionString)
                       .WithResolver(new CommonTypesResolver())
                       .WithObjectHandler(new ObjectHandler())
                       .Build(), ct
               ))
{
   
    i++;
}

public class ObjectHandler: ISubcriptionObjectHandler
{
    public Task<object> Handle(object @event)
    {
        switch (@event)
        {
            case UserCreated c:
                Console.WriteLine(JsonSerialization.ToJson(c, SourceGenerationContext.Default.UserCreated));
                break;
            case UserDeleted d:
                Console.WriteLine(JsonSerialization.ToJson(d, SourceGenerationContext.Default.UserDeleted));
                break;
        }

        return Task.FromResult(@event);
    }
}



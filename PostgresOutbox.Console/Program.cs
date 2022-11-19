using PostgresOutbox.Serialization;
using PostgresOutbox.Subscriptions;


const string connectionString = "PORT = 5432; HOST = localhost; TIMEOUT = 15; POOLING = True; MINPOOLSIZE = 1; MAXPOOLSIZE = 100; COMMANDTIMEOUT = 20; DATABASE = 'postgres'; PASSWORD = 'Password12!'; USER ID = 'postgres'";

var cancellationTokenSource = new CancellationTokenSource();

var ct = cancellationTokenSource.Token;
//
// var @event = new UserCreated(Guid.NewGuid(), Guid.NewGuid().ToString());
// await EventsAppender.AppendAsync(@event, ConnectrionString, ct);

var slotName = "events_slot" + Guid.NewGuid().ToString().Replace("-", "");

var subscriptionOptions = new EventsSubscriptionOptions(connectionString, slotName, "events_pub", "events");
var subscription = new EventsSubscription();

await foreach (var readEvent in subscription.Subscribe(subscriptionOptions, ct))
{
    Console.WriteLine(JsonSerialization.ToJson(readEvent));
}


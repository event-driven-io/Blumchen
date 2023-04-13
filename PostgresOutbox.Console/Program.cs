using PostgresForDotnetDev.CLI;
using PostgresOutbox.Database;
using PostgresOutbox.Serialization;
using PostgresOutbox.Subscriptions;


var cancellationTokenSource = new CancellationTokenSource();

var ct = cancellationTokenSource.Token;

var slotName = "events_slot" + Guid.NewGuid().ToString().Replace("-", "");

var subscriptionOptions = new EventsSubscriptionOptions(Settings.ConnectionString, slotName, "events_pub", "trips" , new EventDataMapper());
var subscription = new EventsSubscription();

await foreach (var readEvent in subscription.Subscribe(subscriptionOptions, ct:ct))
{
    Console.WriteLine(JsonSerialization.ToJson(readEvent));
}


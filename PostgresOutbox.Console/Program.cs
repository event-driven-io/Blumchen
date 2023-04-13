using PostgresForDotnetDev.CLI;
using PostgresOutbox.Database;
using PostgresOutbox.Serialization;
using PostgresOutbox.Subscriptions;
using PostgresOutbox.Subscriptions.Replication;


var cancellationTokenSource = new CancellationTokenSource();

var ct = cancellationTokenSource.Token;

var slotName = "events_slot" + Guid.NewGuid().ToString().Replace("-", "");

var subscriptionOptions = new SubscriptionOptions(Settings.ConnectionString, slotName, "events_pub", "events" , new EventDataMapper());
var subscription = new Subscription();

await foreach (var readEvent in subscription.Subscribe(subscriptionOptions, ct:ct))
{
    Console.WriteLine(JsonSerialization.ToJson(readEvent));
}


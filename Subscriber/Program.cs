using Commons;
using PostgresOutbox.Serialization;
using PostgresOutbox.Subscriptions;
using PostgresOutbox.Subscriptions.Replication;

using static PostgresOutbox.Subscriptions.Management.PublicationManagement;
using static PostgresOutbox.Subscriptions.Management.ReplicationSlotManagement;

#pragma warning disable CS8601 // Possible null reference assignment.
Console.Title = typeof(Program).Assembly.GetName().Name;
#pragma warning restore CS8601 // Possible null reference assignment.
var cancellationTokenSource = new CancellationTokenSource();

var ct = cancellationTokenSource.Token;

var slotName = "events_slot" + Guid.NewGuid().ToString().Replace("-", "");

var subscriptionOptions = new SubscriptionOptions(
    Settings.ConnectionString,
    new PublicationSetupOptions("events_pub","events" ),
    new ReplicationSlotSetupOptions(slotName),
    new EventDataMapper()
);

var subscription = new Subscription();

await foreach (var readEvent in subscription.Subscribe(subscriptionOptions, ct:ct))
{
    Console.WriteLine(JsonSerialization.ToJson(readEvent));
}


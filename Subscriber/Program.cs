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

await foreach (var readEvent in new Subscription().Subscribe(new SubscriptionOptions(
                   Settings.ConnectionString,
                   new PublicationSetupOptions("events_pub","events" ),
                   new ReplicationSlotSetupOptions("events_slot"),
                   new EventDataMapper()
               ), ct:ct))
{
    Console.WriteLine(JsonSerialization.ToJson(readEvent));
}


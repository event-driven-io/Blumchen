using JetBrains.Annotations;
using PostgresOutbox.Subscriptions.Replication;
using static PostgresOutbox.Subscriptions.Management.PublicationManagement;
using static PostgresOutbox.Subscriptions.Management.ReplicationSlotManagement;

namespace PostgresOutbox.Subscriptions;

internal interface ISubscriptionOptions
{
    [UsedImplicitly] string ConnectionString { get; }
    IReplicationDataMapper DataMapper { get; }
    PublicationSetupOptions PublicationOptions { get; }
    [UsedImplicitly] ReplicationSlotSetupOptions ReplicationOptions { get; }
    [UsedImplicitly] IErrorProcessor ErrorProcessor { get; }

    void Deconstruct(
        out string connectionString,
        out PublicationSetupOptions publicationSetupOptions,
        out ReplicationSlotSetupOptions replicationSlotSetupOptions,
        out IErrorProcessor errorProcessor,
        out IReplicationDataMapper dataMapper,
        out Dictionary<Type, IConsume> registry);
}

internal record SubscriptionOptions(
    string ConnectionString,
    PublicationSetupOptions PublicationOptions,
    ReplicationSlotSetupOptions ReplicationOptions,
    IErrorProcessor ErrorProcessor,
    IReplicationDataMapper DataMapper,
    Dictionary<Type, IConsume> Registry): ISubscriptionOptions;

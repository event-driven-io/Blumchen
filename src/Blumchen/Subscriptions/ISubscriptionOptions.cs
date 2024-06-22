using Blumchen.Subscriptions.Replication;
using JetBrains.Annotations;
using static Blumchen.Subscriptions.Management.PublicationManagement;
using static Blumchen.Subscriptions.Management.ReplicationSlotManagement;

namespace Blumchen.Subscriptions;

internal interface ISubscriptionOptions
{
    [UsedImplicitly] string ConnectionString { get; }
    IReplicationDataMapper DataMapper { get; }
    [UsedImplicitly] PublicationSetupOptions PublicationOptions { get; }
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

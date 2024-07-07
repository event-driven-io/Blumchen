using Blumchen.Subscriptions.Replication;
using JetBrains.Annotations;
using Npgsql;
using static Blumchen.Subscriptions.Management.PublicationManagement;
using static Blumchen.Subscriptions.Management.ReplicationSlotManagement;

namespace Blumchen.Subscriptions;

public interface ISubscriptionOptions
{
    [UsedImplicitly] NpgsqlDataSource DataSource { get; }
    [UsedImplicitly] NpgsqlConnectionStringBuilder ConnectionStringBuilder { get; }
    IReplicationDataMapper DataMapper { get; }
    [UsedImplicitly] PublicationSetupOptions PublicationOptions { get; }
    [UsedImplicitly] ReplicationSlotSetupOptions ReplicationOptions { get; }
    [UsedImplicitly] IErrorProcessor ErrorProcessor { get; }

    void Deconstruct(
        out NpgsqlDataSource dataSource,
        out NpgsqlConnectionStringBuilder connectionStringBuilder,
        out PublicationSetupOptions publicationSetupOptions,
        out ReplicationSlotSetupOptions replicationSlotSetupOptions,
        out IErrorProcessor errorProcessor,
        out IReplicationDataMapper dataMapper,
        out Dictionary<Type, IMessageHandler> registry);
}

internal record SubscriptionOptions(
    NpgsqlDataSource DataSource,
    NpgsqlConnectionStringBuilder ConnectionStringBuilder,
    PublicationSetupOptions PublicationOptions,
    ReplicationSlotSetupOptions ReplicationOptions,
    IErrorProcessor ErrorProcessor,
    IReplicationDataMapper DataMapper,
    Dictionary<Type, IMessageHandler> Registry): ISubscriptionOptions;

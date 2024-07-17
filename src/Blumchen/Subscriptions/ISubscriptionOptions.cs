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
    IDictionary<string, Tuple<IReplicationJsonBMapper, IMessageHandler>> Registry { get; }
    [UsedImplicitly] PublicationSetupOptions PublicationOptions { get; }
    [UsedImplicitly] ReplicationSlotSetupOptions ReplicationOptions { get; }
    [UsedImplicitly] IErrorProcessor ErrorProcessor { get; }

    void Deconstruct(
        out NpgsqlDataSource dataSource,
        out NpgsqlConnectionStringBuilder connectionStringBuilder,
        out PublicationSetupOptions publicationSetupOptions,
        out ReplicationSlotSetupOptions replicationSlotSetupOptions,
        out IErrorProcessor errorProcessor,
        out IDictionary<string, Tuple<IReplicationJsonBMapper, IMessageHandler>> registry);
}

internal record SubscriptionOptions(
    NpgsqlDataSource DataSource,
    NpgsqlConnectionStringBuilder ConnectionStringBuilder,
    PublicationSetupOptions PublicationOptions,
    ReplicationSlotSetupOptions ReplicationOptions,
    IErrorProcessor ErrorProcessor,
    IDictionary<string, Tuple<IReplicationJsonBMapper, IMessageHandler>> Registry): ISubscriptionOptions;

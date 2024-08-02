using System.Reflection;
using Blumchen.Subscriptions;
using Blumchen.Subscriptions.Replication;
using JetBrains.Annotations;
using Npgsql;
using static Blumchen.Subscriptions.Management.PublicationManagement;
using static Blumchen.Subscriptions.Management.ReplicationSlotManagement;

namespace Blumchen.Subscriber;

public interface ISubscriberOptions
{
    [UsedImplicitly] NpgsqlDataSource DataSource { get; }
    [UsedImplicitly] NpgsqlConnectionStringBuilder ConnectionStringBuilder { get; }
    IDictionary<string, Tuple<IReplicationJsonBMapper, IMessageHandler, MethodInfo>> Registry { get; }
    [UsedImplicitly] PublicationOptions PublicationOptions { get; }
    [UsedImplicitly] ReplicationSlotOptions ReplicationOptions { get; }
    [UsedImplicitly] IErrorProcessor ErrorProcessor { get; }

    void Deconstruct(
        out NpgsqlDataSource dataSource,
        out NpgsqlConnectionStringBuilder connectionStringBuilder,
        out PublicationOptions publicationOptions,
        out ReplicationSlotOptions replicationSlotOptions,
        out IErrorProcessor errorProcessor,
        out IDictionary<string, Tuple<IReplicationJsonBMapper, IMessageHandler, MethodInfo>> registry);
}

internal record SubscriberOptions(
    NpgsqlDataSource DataSource,
    NpgsqlConnectionStringBuilder ConnectionStringBuilder,
    PublicationOptions PublicationOptions,
    ReplicationSlotOptions ReplicationOptions,
    IErrorProcessor ErrorProcessor,
    IDictionary<string, Tuple<IReplicationJsonBMapper, IMessageHandler, MethodInfo>> Registry): ISubscriberOptions;

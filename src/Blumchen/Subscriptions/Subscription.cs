using System.Reflection;
using System.Runtime.CompilerServices;
using Blumchen.Database;
using Blumchen.Subscriptions.Management;
using Blumchen.Subscriptions.Replication;
using Npgsql;
using Npgsql.Replication;
using Npgsql.Replication.PgOutput;
using Npgsql.Replication.PgOutput.Messages;

namespace Blumchen.Subscriptions;

using static PublicationManagement;
using static ReplicationSlotManagement;
using static ReplicationSlotManagement.CreateReplicationSlotResult;

public sealed class Subscription: IAsyncDisposable
{
    private readonly Func<string, IMessageHandler, Type, (IMessageHandler messageHandler, MethodInfo methodInfo)> _memoizer = Memoizer<string, IMessageHandler, Type, (IMessageHandler messageHandler,
        MethodInfo methodInfo)>.Execute(MessageHandler);

    public enum CreateStyle
    {
        WhenNotExists,
        AlwaysRecreate,
        Never
    }

    private LogicalReplicationConnection? _connection;

    private readonly SubscriptionOptionsBuilder _builder = new();

    public async IAsyncEnumerable<IEnvelope> Subscribe(
        Func<SubscriptionOptionsBuilder, SubscriptionOptionsBuilder> builder,
        [EnumeratorCancellation] CancellationToken ct = default
    )
    {
        await foreach (var _ in Subscribe(builder(_builder).Build(), ct))
            yield return _;
    }

    internal async IAsyncEnumerable<IEnvelope> Subscribe(
       ISubscriptionOptions subscriptionOptions,
       [EnumeratorCancellation] CancellationToken ct = default
   )
    {
        var (dataSource, connectionStringBuilder, publicationSetupOptions, replicationSlotSetupOptions, errorProcessor, registry) = subscriptionOptions;
        await dataSource.EnsureTableExists(publicationSetupOptions.TableDescriptor, ct).ConfigureAwait(false);

        _connection = new LogicalReplicationConnection(connectionStringBuilder.ConnectionString);
        await _connection.Open(ct).ConfigureAwait(false);

        await dataSource.SetupPublication(publicationSetupOptions, ct).ConfigureAwait(false);
        var result = await dataSource.SetupReplicationSlot(_connection, replicationSlotSetupOptions, ct).ConfigureAwait(false);
        var replicationDataMapper = new ReplicationDataMapper(registry);
        PgOutputReplicationSlot slot;

        if (result is not Created created)
        {
            slot = new PgOutputReplicationSlot(replicationSlotSetupOptions.SlotName);
        }
        else
        {
            slot = new PgOutputReplicationSlot(
                new ReplicationSlotOptions(
                    replicationSlotSetupOptions.SlotName,
                    created.LogSequenceNumber
                )
            );

            await foreach (var envelope in ReadExistingRowsFromSnapshot(dataSource, created.SnapshotName, replicationDataMapper, publicationSetupOptions.TableDescriptor, publicationSetupOptions.RegisteredTypes,  ct).ConfigureAwait(false))
            {
                await foreach (var subscribe in ProcessEnvelope(envelope, registry, errorProcessor).WithCancellation(ct).ConfigureAwait(false))
                    yield return subscribe;
            }
        }

        await foreach (var message in
                       _connection.StartReplication(slot,
                           new PgOutputReplicationOptions(publicationSetupOptions.PublicationName, 1, replicationSlotSetupOptions.Binary), ct).ConfigureAwait(false))
        {
            if (message is InsertMessage insertMessage)
            {
                var envelope = await replicationDataMapper.ReadFromReplication(insertMessage, ct).ConfigureAwait(false);
                await foreach (var subscribe in ProcessEnvelope(envelope, registry, errorProcessor).WithCancellation(ct).ConfigureAwait(false))
                    yield return subscribe;
            }
            // Always call SetReplicationStatus() or assign LastAppliedLsn and LastFlushedLsn individually
            // so that Npgsql can inform the server which WAL files can be removed/recycled.
            _connection.SetReplicationStatus(message.WalEnd);
            await _connection.SendStatusUpdate(ct).ConfigureAwait(false);
        }
    }

    private async IAsyncEnumerable<IEnvelope> ProcessEnvelope(
        IEnvelope envelope,
        IDictionary<string, Tuple<IReplicationJsonBMapper, IMessageHandler>> registry,
        IErrorProcessor errorProcessor
    )
    {
        switch (envelope)
        {
            case KoEnvelope error:
                await errorProcessor.Process(error.Error).ConfigureAwait(false);
                yield break;
            case OkEnvelope(var value, var messageType):
            {
                var objType = value.GetType();
                    var (messageHandler, methodInfo) = _memoizer(messageType, registry[messageType].Item2, objType);
                    await ((Task)methodInfo.Invoke(messageHandler, [value])!).ConfigureAwait(false);

                    yield return envelope;
                yield break;
            }
        }
    }

    private static (IMessageHandler messageHandler, MethodInfo methodInfo) MessageHandler(
        string messageType, IMessageHandler messageHandler, Type objType)
    {
        var methodInfos = messageHandler.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public);
        var methodInfo = methodInfos.SingleOrDefault(mi => mi.GetParameters().Any(pa => pa.ParameterType == objType))
                         ?? throw new NotSupportedException($"Unregistered type for {objType.AssemblyQualifiedName}");
        return (messageHandler, methodInfo);
    }

    private static async IAsyncEnumerable<IEnvelope> ReadExistingRowsFromSnapshot(
        NpgsqlDataSource dataSource,
        string snapshotName,
        ReplicationDataMapper dataMapper,
        TableDescriptorBuilder.MessageTable tableDescriptor,
        ISet<string> registeredTypes,
    [EnumeratorCancellation] CancellationToken ct = default
    )
    {
        var connection = await dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var connection1 = connection.ConfigureAwait(false);
        await foreach (var row in connection.GetRowsFromSnapshot(
                           snapshotName,
                           tableDescriptor,
                           dataMapper,
                           registeredTypes,
                           ct).ConfigureAwait(false))
            yield return row;
    }

    public async ValueTask DisposeAsync()
    {
        if(_connection != null)
            await _connection.DisposeAsync().ConfigureAwait(false);
    }
}

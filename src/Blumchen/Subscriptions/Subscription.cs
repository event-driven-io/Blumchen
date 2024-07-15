using System.Reflection;
using System.Runtime.CompilerServices;
using Blumchen.Database;
using Blumchen.Serialization;
using Blumchen.Subscriptions.Management;
using Blumchen.Subscriptions.ReplicationMessageHandlers;
using Blumchen.Subscriptions.SnapshotReader;
using Microsoft.Extensions.Logging;
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
    public enum CreateStyle
    {
        WhenNotExists,
        AlwaysRecreate,
        Never
    }
    private LogicalReplicationConnection? _connection;
    private readonly SubscriptionOptionsBuilder _builder = new();
    private ISubscriptionOptions? _options;
    public async IAsyncEnumerable<IEnvelope> Subscribe(
        Func<SubscriptionOptionsBuilder, SubscriptionOptionsBuilder> builder,
        [EnumeratorCancellation] CancellationToken ct = default
    )
    {
        _options = builder(_builder).Build();
        var (dataSource, connectionStringBuilder, publicationSetupOptions, replicationSlotSetupOptions, errorProcessor, replicationDataMapper, registry) = _options;
        
        await dataSource.EnsureTableExists(publicationSetupOptions.TableDescriptor, ct).ConfigureAwait(false);

        _connection = new LogicalReplicationConnection(connectionStringBuilder.ConnectionString);
        await _connection.Open(ct).ConfigureAwait(false);

        await dataSource.SetupPublication(publicationSetupOptions, ct).ConfigureAwait(false);
        var result = await dataSource.SetupReplicationSlot(_connection, replicationSlotSetupOptions, ct).ConfigureAwait(false);

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

            await foreach (var envelope in ReadExistingRowsFromSnapshot(dataSource, created.SnapshotName, _options, ct).ConfigureAwait(false))
            await foreach (var subscribe in ProcessEnvelope<IEnvelope>(envelope, registry, errorProcessor).WithCancellation(ct).ConfigureAwait(false))
                yield return subscribe;
        }

        await foreach (var message in
                       _connection.StartReplication(slot,
                           new PgOutputReplicationOptions(publicationSetupOptions.PublicationName, 1, replicationSlotSetupOptions.Binary), ct).ConfigureAwait(false))
        {
            if (message is InsertMessage insertMessage)
            {
                var envelope = await replicationDataMapper.ReadFromReplication(insertMessage, ct).ConfigureAwait(false);
                await foreach (var subscribe in ProcessEnvelope<IEnvelope>(envelope, registry, errorProcessor).WithCancellation(ct).ConfigureAwait(false))
                    yield return subscribe;
            }
            // Always call SetReplicationStatus() or assign LastAppliedLsn and LastFlushedLsn individually
            // so that Npgsql can inform the server which WAL files can be removed/recycled.
            _connection.SetReplicationStatus(message.WalEnd);
            await _connection.SendStatusUpdate(ct).ConfigureAwait(false);
        }
    }

    private static async IAsyncEnumerable<T> ProcessEnvelope<T>(
        IEnvelope envelope,
        Dictionary<Type, IHandler> registry,
        IErrorProcessor errorProcessor
    ) where T:class
    {
        switch (envelope)
        {
            case KoEnvelope error:
                await errorProcessor.Process(error.Error).ConfigureAwait(false);
                yield break;
            case OkEnvelope okEnvelope:
            {
                var obj = okEnvelope.Value;
                var objType = obj.GetType();
                var (consumer, methodInfo) = Memoize(registry, objType, Consumer);
                await ((Task)methodInfo.Invoke(consumer, [obj])!).ConfigureAwait(false);
                yield return (T)envelope;
                yield break;
            }
        }
    }

    private static readonly Dictionary<Type, (IHandler consumer, MethodInfo methodInfo)> Cache = [];


    private static (IHandler consumer, MethodInfo methodInfo) Memoize
    (
        Dictionary<Type, IHandler> registry,
        Type objType,
        Func<Dictionary<Type, IHandler>, Type, (IHandler consumer, MethodInfo methodInfo)> func
    )
    {
        if (!Cache.TryGetValue(objType, out var entry))
            entry = func(registry, objType);
        Cache[objType] = entry;
        return entry;
    }
    private static (IHandler consumer, MethodInfo methodInfo) Consumer(Dictionary<Type, IHandler> registry, Type objType)
    {
        var consumer = registry[objType] ?? throw new NotSupportedException($"Unregistered type for {objType.AssemblyQualifiedName}");
        var methodInfos = consumer.GetType().GetMethods(BindingFlags.Instance|BindingFlags.Public);
        var methodInfo = methodInfos.SingleOrDefault(mi=>mi.GetParameters().Any(pa => pa.ParameterType == objType))
                         ?? throw new NotSupportedException($"Unregistered type for {objType.AssemblyQualifiedName}");
        return (consumer, methodInfo);
    }

    private static async IAsyncEnumerable<IEnvelope> ReadExistingRowsFromSnapshot(
        NpgsqlDataSource dataSource,
        string snapshotName,
        ISubscriptionOptions options,
        [EnumeratorCancellation] CancellationToken ct = default
    )
    {
        var connection = await dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var connection1 = connection.ConfigureAwait(false);
        await foreach (var row in connection.GetRowsFromSnapshot(
                           snapshotName,
                           options.PublicationOptions.TableDescriptor,
                           options.DataMapper,
                           options.PublicationOptions.TypeResolver.Keys().ToHashSet(),
                           ct).ConfigureAwait(false))
            yield return row;
    }

    public async ValueTask DisposeAsync()
    {
        if(_connection != null)
            await _connection.DisposeAsync().ConfigureAwait(false);
    }
}

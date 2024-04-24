using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Npgsql;
using Npgsql.Replication;
using Npgsql.Replication.PgOutput;
using Npgsql.Replication.PgOutput.Messages;
using PostgresOutbox.Subscriptions.Management;
using PostgresOutbox.Subscriptions.ReplicationMessageHandlers;
using PostgresOutbox.Subscriptions.SnapshotReader;
using PostgresOutbox.Table;

namespace PostgresOutbox.Subscriptions;

using static PublicationManagement;
using static ReplicationSlotManagement;
using static ReplicationSlotManagement.CreateReplicationSlotResult;

public interface ISubscription
{
    IAsyncEnumerable<Task<IEnvelope>> Subscribe(
        Func<SubscriptionOptionsBuilder,ISubscriptionOptions> builder,
        ILoggerFactory? loggerFactory,
        CancellationToken ct);
}

public enum CreateStyle
{
    WhenNotExists,
    AlwaysRecreate,
    Never
}

public sealed class Subscription: ISubscription, IAsyncDisposable
{
    private static LogicalReplicationConnection? _connection;
    private static readonly SubscriptionOptionsBuilder Builder = new();
    public async IAsyncEnumerable<Task<IEnvelope>> Subscribe(
        Func<SubscriptionOptionsBuilder, ISubscriptionOptions> builder,
        ILoggerFactory? loggerFactory = null,
        [EnumeratorCancellation] CancellationToken ct = default
    )
    {
        _options = builder(Builder);
        var (connectionString, publicationSetupOptions, slotSetupOptions, errorProcessor, replicationDataMapper, registry) = _options;
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.UseLoggerFactory(loggerFactory);

        var dataSource = dataSourceBuilder.Build();
        await EventTable.Ensure(dataSource, publicationSetupOptions.TableName, ct);

        _connection = new LogicalReplicationConnection(connectionString);
        await _connection.Open(ct);


        await dataSource.SetupPublication(publicationSetupOptions, ct);
        var result = await dataSource.SetupReplicationSlot(_connection, slotSetupOptions, ct);

        PgOutputReplicationSlot slot;

        if (result is not Created created)
        {
            slot = new PgOutputReplicationSlot(slotSetupOptions.SlotName);
        }
        else
        {
            slot = new PgOutputReplicationSlot(
                new ReplicationSlotOptions(
                    slotSetupOptions.SlotName,
                    created.LogSequenceNumber
                )
            );

            await foreach (var envelope in ReadExistingRowsFromSnapshot(dataSource, created.SnapshotName, _options, ct))
            await foreach (var subscribe in ProcessEnvelope<IEnvelope>(envelope, registry, errorProcessor).WithCancellation(ct))
                yield return subscribe;
        }

        await foreach (var message in
                       _connection.StartReplication(slot,
                           new PgOutputReplicationOptions(publicationSetupOptions.PublicationName, 1, slotSetupOptions.Binary), ct))
        {
            if (message is InsertMessage insertMessage)
            {
                var envelope = await replicationDataMapper.ReadFromReplication(insertMessage, ct);
                await foreach (var subscribe in ProcessEnvelope<IEnvelope>(envelope, registry, errorProcessor).WithCancellation(ct))
                    yield return subscribe;
            }
            // Always call SetReplicationStatus() or assign LastAppliedLsn and LastFlushedLsn individually
            // so that Npgsql can inform the server which WAL files can be removed/recycled.
            _connection.SetReplicationStatus(message.WalEnd);
            await _connection.SendStatusUpdate(ct);
        }
    }

    private static async IAsyncEnumerable<Task<T>> ProcessEnvelope<T>(
        IEnvelope envelope,
        Dictionary<Type, IConsume> registry,
        IErrorProcessor errorProcessor
    ) where T:class
    {
        switch (envelope)
        {
            case KoEnvelope error:
                await errorProcessor.Process(error.Error);
                yield break;
            case OkEnvelope okEnvelope:
            {
                var obj = okEnvelope.Value;
                var objType = obj.GetType();
                var (consumer, methodInfo) = Memoize(registry, objType, Consumer);
                yield return (methodInfo.Invoke(consumer, [obj]) as Task<T>)!;
                break;
            }
        }
    }

    private static readonly Dictionary<Type, (IConsume consumer, MethodInfo methodInfo)> Cache = [];
    private ISubscriptionOptions? _options;

    private static (IConsume consumer, MethodInfo methodInfo) Memoize
    (
        Dictionary<Type, IConsume> registry,
        Type objType,
        Func<Dictionary<Type, IConsume>, Type, (IConsume consumer, MethodInfo methodInfo)> func
    )
    {
        if (!Cache.TryGetValue(objType, out var entry))
            entry = func(registry, objType);
        Cache[objType] = entry;
        return entry;
    }
    private static (IConsume consumer, MethodInfo methodInfo) Consumer(Dictionary<Type, IConsume> registry, Type objType)
    {
        var consumer = registry[objType] ?? throw new NotSupportedException($"Unregistered type for {objType.AssemblyQualifiedName}");
        var methodInfos = consumer.GetType().GetMethods(BindingFlags.Instance|BindingFlags.Public);
        var methodInfo = methodInfos?.SingleOrDefault(mi=>mi.GetParameters().Any(pa => pa.ParameterType == objType))
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
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await foreach (var row in connection.GetRowsFromSnapshot(
                           snapshotName,
                           options.PublicationOptions.TableName,
                           options.DataMapper,
                           ct))
            yield return row;
    }

    public async ValueTask DisposeAsync() => await _connection!.DisposeAsync();
}
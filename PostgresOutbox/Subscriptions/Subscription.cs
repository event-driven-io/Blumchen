using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using Npgsql;
using Npgsql.Replication;
using Npgsql.Replication.PgOutput;
using Npgsql.Replication.PgOutput.Messages;
using PostgresOutbox.Subscriptions.Management;
using PostgresOutbox.Subscriptions.Replication;
using PostgresOutbox.Subscriptions.ReplicationMessageHandlers;
using PostgresOutbox.Subscriptions.SnapshotReader;

namespace PostgresOutbox.Subscriptions;

using static PublicationManagement;
using static ReplicationSlotManagement;
using static ReplicationSlotManagement.CreateReplicationSlotResult;

public interface ISubscription
{
    IAsyncEnumerable<object> Subscribe(Func<SubscriptionOptionsBuilder, ISubscriptionOptions> builder, CancellationToken ct);
}

public interface ISubscriptionOptions
{
    [UsedImplicitly] string ConnectionString { get; }
    IReplicationDataMapper DataMapper { get; }
    PublicationSetupOptions PublicationOptions { get; }
    [UsedImplicitly] ReplicationSlotSetupOptions ReplicationOptions { get; }

    void Deconstruct(
        out string connectionString,
        out PublicationSetupOptions publicationSetupOptions,
        out ReplicationSlotSetupOptions replicationSlotSetupOptions,
        out IReplicationDataMapper dataMapper);
}

internal record SubscriptionOptions(
    string ConnectionString,
    PublicationSetupOptions PublicationOptions,
    ReplicationSlotSetupOptions ReplicationOptions,
    IReplicationDataMapper DataMapper
    ): ISubscriptionOptions;


public class SubscriptionOptionsBuilder
{
    private static string? _connectionString;
    private static PublicationSetupOptions? _publicationSetupOptions;
    private static ReplicationSlotSetupOptions? _slotOptions;
    private static IReplicationDataMapper? _dataMapper;


    static SubscriptionOptionsBuilder()
    {
        _connectionString = null;
        _publicationSetupOptions = default;
        _slotOptions = default;
        _dataMapper = default;
    }

    public SubscriptionOptionsBuilder WithConnectionString(string connectionString)
    {
        _connectionString = connectionString;
        return this;
    }

    public SubscriptionOptionsBuilder WithMapper(IReplicationDataMapper dataMapper)
    {
        _dataMapper = dataMapper;
        return this;
    }

    public SubscriptionOptionsBuilder WitPublicationOptions(PublicationSetupOptions publicationSetupOptions)
    {
        _publicationSetupOptions = publicationSetupOptions;
        return this;
    }

    public SubscriptionOptionsBuilder WithReplicationOptions(ReplicationSlotSetupOptions replicationSlotOptions)
    {
        _slotOptions = replicationSlotOptions;
        return this;
    }

    public ISubscriptionOptions Build()
    {
        ArgumentNullException.ThrowIfNull(_connectionString);
        ArgumentNullException.ThrowIfNull(_dataMapper);
        return new SubscriptionOptions(
            _connectionString,
            _publicationSetupOptions ?? new PublicationSetupOptions(),
            _slotOptions ?? new ReplicationSlotSetupOptions(),
            _dataMapper);
    }
}



public enum CreateStyle
{
    WhenNotExists,
    AlwaysRecreate,
    Never
}

public class Subscription: ISubscription
{
    private static readonly SubscriptionOptionsBuilder Builder = new();
    public async IAsyncEnumerable<object> Subscribe(
        Func<SubscriptionOptionsBuilder, ISubscriptionOptions> builder,
        [EnumeratorCancellation] CancellationToken ct = default
    )
    {
        var options = builder(Builder);
        var (connectionString, publicationSetupOptions, slotSetupOptions, replicationDataMapper) = options;
        var dataSource = NpgsqlDataSource.Create(connectionString);

        await using var conn = new LogicalReplicationConnection(connectionString);
        await conn.Open(ct);

        await dataSource.SetupPublication(publicationSetupOptions, ct);
        var result = await dataSource.SetupReplicationSlot(conn, slotSetupOptions, ct);

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

            await foreach (var @event in ReadExistingRowsFromSnapshot(dataSource, created.SnapshotName, options, ct))
                yield return @event;
        }

        await foreach (var message in
                       conn.StartReplication(slot,
                           new PgOutputReplicationOptions(publicationSetupOptions.PublicationName, 1), ct))
        {
            if (message is InsertMessage insertMessage)
                yield return await InsertMessageHandler.Handle(insertMessage, replicationDataMapper, ct);

            // Always call SetReplicationStatus() or assign LastAppliedLsn and LastFlushedLsn individually
            // so that Npgsql can inform the server which WAL files can be removed/recycled.
            conn.SetReplicationStatus(message.WalEnd);
            await conn.SendStatusUpdate(ct);
        }
    }

    private static async IAsyncEnumerable<object> ReadExistingRowsFromSnapshot(
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
}

using JetBrains.Annotations;
using PostgresOutbox.Serialization;
using PostgresOutbox.Subscriptions.Management;
using PostgresOutbox.Subscriptions.Replication;

namespace PostgresOutbox.Subscriptions;

public sealed class SubscriptionOptionsBuilder
{
    private static string? _connectionString;
    private static PublicationManagement.PublicationSetupOptions? _publicationSetupOptions;
    private static ReplicationSlotManagement.ReplicationSlotSetupOptions? _slotOptions;
    private static IReplicationDataMapper? _dataMapper;

    static SubscriptionOptionsBuilder()
    {
        _connectionString = null;
        _publicationSetupOptions = default;
        _slotOptions = default;
        _dataMapper = default;
    }

    public SubscriptionOptionsBuilder ConnectionString(string connectionString)
    {
        _connectionString = connectionString;
        return this;
    }

    public SubscriptionOptionsBuilder TypeResolver(ITypeResolver resolver)
    {
        _dataMapper = new ReplicationDataMapper(resolver);
        return this;
    }

    [UsedImplicitly]
    public SubscriptionOptionsBuilder WithMapper(IReplicationDataMapper dataMapper)
    {
        _dataMapper = dataMapper;
        return this;
    }

    [UsedImplicitly]
    public SubscriptionOptionsBuilder WithPublicationOptions(PublicationManagement.PublicationSetupOptions publicationSetupOptions)
    {
        _publicationSetupOptions = publicationSetupOptions;
        return this;
    }

    [UsedImplicitly]
    public SubscriptionOptionsBuilder WithReplicationOptions(ReplicationSlotManagement.ReplicationSlotSetupOptions replicationSlotOptions)
    {
        _slotOptions = replicationSlotOptions;
        return this;
    }

    private readonly Dictionary<Type, IConsume> _registry = [];
    private IErrorProcessor? _errorProcessor;

    public SubscriptionOptionsBuilder Consumes<T, TU>(TU consumer) where T : class
        where TU : class, IConsumes<T>
    {
        _registry.TryAdd(typeof(T), consumer);
        return this;
    }

    public SubscriptionOptionsBuilder WithErrorProcessor(IErrorProcessor? errorProcessor)
    {
        _errorProcessor = errorProcessor;
        return this;
    }

    internal ISubscriptionOptions Build()
    {
        ArgumentNullException.ThrowIfNull(_connectionString);
        ArgumentNullException.ThrowIfNull(_dataMapper);
        if(_registry.Count == 0)_registry.Add(typeof(object), new ObjectTracingConsumer());

        return new SubscriptionOptions(
            _connectionString,
            _publicationSetupOptions ?? new PublicationManagement.PublicationSetupOptions(),
            _slotOptions ?? new ReplicationSlotManagement.ReplicationSlotSetupOptions(),
            _errorProcessor ?? new ConsoleOutErrorProcessor(),
            _dataMapper,
            _registry);
    }
}

public class ObjectTracingConsumer: IConsumes<object>
{
    private static ulong _counter = 0;
    public Task Handle(object value)
    {
        Interlocked.Increment(ref _counter);
        return Console.Out.WriteLineAsync();
    }
}

public interface IErrorProcessor
{
    Func<Exception, Task> Process { get; }
}

public record ConsoleOutErrorProcessor: IErrorProcessor
{
    public Func<Exception, Task> Process => exception => Console.Out.WriteLineAsync($"record id:{0} resulted in error:{exception.Message}");
}

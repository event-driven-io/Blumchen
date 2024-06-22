using Blumchen.Serialization;
using Blumchen.Subscriptions.Management;
using Blumchen.Subscriptions.Replication;
using JetBrains.Annotations;

namespace Blumchen.Subscriptions;
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

public sealed class SubscriptionOptionsBuilder
{
    private static string? _connectionString;
    private static PublicationManagement.PublicationSetupOptions _publicationSetupOptions;
    private static ReplicationSlotManagement.ReplicationSlotSetupOptions? _slotOptions;
    private static IReplicationDataMapper? _dataMapper;

    static SubscriptionOptionsBuilder()
    {
        _connectionString = null;
        _publicationSetupOptions = new();
        _slotOptions = default;
        _dataMapper = default;
    }

    [UsedImplicitly]
    public SubscriptionOptionsBuilder ConnectionString(string connectionString)
    {
        _connectionString = connectionString;
        return this;
    }

    [UsedImplicitly]
    public SubscriptionOptionsBuilder TypeResolver(ITypeResolver resolver)
    {
        _dataMapper = new ReplicationDataMapper(resolver);
        _publicationSetupOptions = _publicationSetupOptions with{ TypeResolver = resolver };
        return this;
    }

    [UsedImplicitly]
    public SubscriptionOptionsBuilder WithMapper(IReplicationDataMapper dataMapper)
    {
        _dataMapper = dataMapper;
        return this;
    }

    [UsedImplicitly]
    public SubscriptionOptionsBuilder WithPublicationOptions(PublicationManagement.PublicationSetupOptions publicationOptions)
    {
        _publicationSetupOptions =
            publicationOptions with { TypeResolver = _publicationSetupOptions.TypeResolver };
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

    [UsedImplicitly]
    public SubscriptionOptionsBuilder Consumes<T, TU>(TU consumer) where T : class
        where TU : class, IConsumes<T>
    {
        _registry.TryAdd(typeof(T), consumer);
        return this;
    }

    [UsedImplicitly]
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
            _publicationSetupOptions,
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

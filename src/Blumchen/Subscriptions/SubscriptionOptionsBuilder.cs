using Blumchen.Serialization;
using Blumchen.Subscriptions.Management;
using Blumchen.Subscriptions.Replication;
using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace Blumchen.Subscriptions;
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
public sealed class SubscriptionOptionsBuilder
{
    private static string? _connectionString;
    private static PublicationManagement.PublicationSetupOptions _publicationSetupOptions;
    private static ReplicationSlotManagement.ReplicationSlotSetupOptions? _replicationSlotSetupOptions;
    private static IReplicationDataMapper? _dataMapper;
    private readonly Dictionary<Type, IConsume> _registry = [];
    private IErrorProcessor? _errorProcessor;
    private INamingPolicy? _namingPolicy;
    private JsonSerializerContext? _jsonSerializerContext;
    private static readonly TableDescriptorBuilder TableDescriptorBuilder = new();
    private TableDescriptorBuilder.MessageTable? _messageTable;


    static SubscriptionOptionsBuilder()
    {
        _connectionString = null;
        _publicationSetupOptions = new();
        _replicationSlotSetupOptions = default;
        _dataMapper = default;
    }

    
    [UsedImplicitly]
    public SubscriptionOptionsBuilder WithTable(
        Func<TableDescriptorBuilder, TableDescriptorBuilder> builder)
    {
        _messageTable = builder(TableDescriptorBuilder).Build();
        return this;
    }

    [UsedImplicitly]
    public SubscriptionOptionsBuilder ConnectionString(string connectionString)
    {
        _connectionString = connectionString;
        return this;
    }

    [UsedImplicitly]
    public SubscriptionOptionsBuilder NamingPolicy(INamingPolicy namingPolicy)
    {
        _namingPolicy = namingPolicy;
        return this;
    }

    [UsedImplicitly]
    public SubscriptionOptionsBuilder JsonContext(JsonSerializerContext jsonSerializerContext)
    {
        _jsonSerializerContext = jsonSerializerContext;
        return this;
    }

    [UsedImplicitly]
    public SubscriptionOptionsBuilder WithPublicationOptions(PublicationManagement.PublicationSetupOptions publicationOptions)
    {
        _publicationSetupOptions =
            publicationOptions with { TypeResolver = _publicationSetupOptions.TypeResolver};
        return this;
    }

    [UsedImplicitly]
    public SubscriptionOptionsBuilder WithReplicationOptions(ReplicationSlotManagement.ReplicationSlotSetupOptions replicationSlotOptions)
    {
        _replicationSlotSetupOptions = replicationSlotOptions;
        return this;
    }

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
        ArgumentNullException.ThrowIfNull(_jsonSerializerContext);
        ArgumentNullException.ThrowIfNull(_messageTable);

        var typeResolver = new JsonTypeResolver(_jsonSerializerContext, _namingPolicy);
        foreach (var type in _registry.Keys) typeResolver.WhiteList(type);
        _dataMapper = new ReplicationDataMapper(typeResolver);
        _publicationSetupOptions = _publicationSetupOptions with { TypeResolver = typeResolver, TableDescriptor = _messageTable};

        Ensure(() =>_registry.Keys.Except(_publicationSetupOptions.TypeResolver.Values()), "Unregistered types:{0}");
        Ensure(() => _publicationSetupOptions.TypeResolver.Values().Except(_registry.Keys), "Unregistered consumer for type:{0}");
        if (_registry.Count == 0)_registry.Add(typeof(object), new ObjectTracingConsumer());
        
        return new SubscriptionOptions(
            _connectionString,
            _publicationSetupOptions,
            _replicationSlotSetupOptions ?? new ReplicationSlotManagement.ReplicationSlotSetupOptions(),
            _errorProcessor ?? new ConsoleOutErrorProcessor(),
            _dataMapper,
            _registry);
        static void Ensure(Func<IEnumerable<Type>> evalFn, string formattedMsg)
        {
            var misses = evalFn().ToArray();
            if (misses.Length > 0) throw new Exception(string.Format(formattedMsg, string.Join(", ", misses.Select(t => $"'{t.Name}'"))));
        }

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

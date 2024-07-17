using Blumchen.Serialization;
using Blumchen.Subscriptions.Management;
using Blumchen.Subscriptions.Replication;
using JetBrains.Annotations;
using Npgsql;
using System.Text.Json.Serialization;

namespace Blumchen.Subscriptions;

public sealed class SubscriptionOptionsBuilder
{
    private NpgsqlConnectionStringBuilder? _connectionStringBuilder;
    private NpgsqlDataSource? _dataSource;
    private PublicationManagement.PublicationSetupOptions _publicationSetupOptions = new();
    private ReplicationSlotManagement.ReplicationSlotSetupOptions? _replicationSlotSetupOptions;
    private readonly Dictionary<Type, IMessageHandler> _typeRegistry = [];
    private readonly Dictionary<string, Tuple<IReplicationJsonBMapper, IMessageHandler>> _replicationDataMapperSelector = [];
    private IErrorProcessor? _errorProcessor;
    private INamingPolicy? _namingPolicy;
    private readonly TableDescriptorBuilder _tableDescriptorBuilder = new();
    private TableDescriptorBuilder.MessageTable? _messageTable;
    private readonly IReplicationJsonBMapper _stringDataMapper = new StringReplicationDataMapper(new StringReplicationDataReader());
    private readonly IReplicationJsonBMapper _objectDataMapper = new ObjectReplicationDataMapper(new ObjectReplicationDataReader());
    private IReplicationJsonBMapper? _jsonDataMapper;
    private JsonSerializerContext? _jsonSerializerContext;


    [UsedImplicitly]
    public SubscriptionOptionsBuilder WithTable(
        Func<TableDescriptorBuilder, TableDescriptorBuilder> builder)
    {
        _messageTable = builder(_tableDescriptorBuilder).Build();
        return this;
    }

    [UsedImplicitly]
    public SubscriptionOptionsBuilder ConnectionString(string connectionString)
    {
        _connectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString);
        return this;
    }

    [UsedImplicitly]
    public SubscriptionOptionsBuilder DataSource(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
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
            publicationOptions with { RegisteredTypes = _publicationSetupOptions.RegisteredTypes};
        return this;
    }

    [UsedImplicitly]
    public SubscriptionOptionsBuilder WithReplicationOptions(ReplicationSlotManagement.ReplicationSlotSetupOptions replicationSlotOptions)
    {
        _replicationSlotSetupOptions = replicationSlotOptions;
        return this;
    }

    [UsedImplicitly]
    public SubscriptionOptionsBuilder Consumes<T>(IMessageHandler<T> handler) where T : class
    {
        _typeRegistry.Add(typeof(T), handler);
        return this;
    }

    [UsedImplicitly]
    public SubscriptionOptionsBuilder ConsumesRowObject<T>(IMessageHandler<object> handler) where T : class
        => ConsumesRow<T>(handler, RawData.Object);

    [UsedImplicitly]
    public SubscriptionOptionsBuilder ConsumesRowString<T>(IMessageHandler<string> handler) where T : class
        => ConsumesRow<T>(handler, RawData.String);

    private SubscriptionOptionsBuilder ConsumesRow<T>(IMessageHandler<string> handler, RawData filter) where T : class
    {
        using var urnEnum = typeof(T)
            .GetCustomAttributes(typeof(RawUrnAttribute), false)
            .OfType<RawUrnAttribute>()
            .Where(attribute => attribute.Data == filter)
            .Select(attribute => attribute.Urn).GetEnumerator();
        while (urnEnum.MoveNext()) _replicationDataMapperSelector.Add(urnEnum.Current.ToString(), new Tuple<IReplicationJsonBMapper, IMessageHandler>(_stringDataMapper, handler));
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
        _messageTable ??= _tableDescriptorBuilder.Build();
        ArgumentNullException.ThrowIfNull(_connectionStringBuilder);
        ArgumentNullException.ThrowIfNull(_dataSource);
        if (_jsonSerializerContext != null)
        {

            var typeResolver = new JsonTypeResolver(_jsonSerializerContext, _namingPolicy);
            foreach (var type in _typeRegistry.Keys)
                typeResolver.WhiteList(type);
            _jsonDataMapper = new JsonReplicationDataMapper(typeResolver, new JsonReplicationDataReader(typeResolver));
            foreach (var (key, value) in typeResolver.RegisteredTypes.Join(_typeRegistry,pair => pair.Value, pair => pair.Key, (pair, valuePair) => (pair.Key, valuePair.Value)))
            {
                _replicationDataMapperSelector.Add(key,new Tuple<IReplicationJsonBMapper, IMessageHandler>(_jsonDataMapper, value));
            }

        }
        
        _publicationSetupOptions = _publicationSetupOptions
            with
            {
                RegisteredTypes  = _replicationDataMapperSelector.Keys.ToHashSet(),
                TableDescriptor = _messageTable
            };
        
        return new SubscriptionOptions(
            _dataSource,
            _connectionStringBuilder,
            _publicationSetupOptions,
            _replicationSlotSetupOptions ?? new ReplicationSlotManagement.ReplicationSlotSetupOptions(),
            _errorProcessor ?? new ConsoleOutErrorProcessor(),
            _replicationDataMapperSelector
            );
        //static void Ensure(Func<IEnumerable<Type>> evalFn, string formattedMsg)
        //{
        //    var misses = evalFn().ToArray();
        //    if (misses.Length > 0) throw new Exception(string.Format(formattedMsg, string.Join(", ", misses.Select(t => $"'{t.Name}'"))));
        //}
    }
}

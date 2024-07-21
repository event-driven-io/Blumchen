using System.Collections;
using System.Text.Json.Serialization;
using Blumchen.Serialization;
using Blumchen.Subscriptions;
using Blumchen.Subscriptions.Management;
using Blumchen.Subscriptions.Replication;
using JetBrains.Annotations;
using Npgsql;

namespace Blumchen.Subscriber;

public sealed class OptionsBuilder
{
    internal const string WildCard = "*";

    [System.Diagnostics.CodeAnalysis.NotNull]
    private NpgsqlConnectionStringBuilder? _connectionStringBuilder = default;

    [System.Diagnostics.CodeAnalysis.NotNull]
    private NpgsqlDataSource? _dataSource = default;

    private PublicationManagement.PublicationOptions _publicationOptions = new();
    private ReplicationSlotManagement.ReplicationSlotOptions? _replicationSlotOptions;
    private readonly Dictionary<Type, IMessageHandler> _typeRegistry = [];

    private readonly Dictionary<string, Tuple<IReplicationJsonBMapper, IMessageHandler>>
        _replicationDataMapperSelector = [];

    private IErrorProcessor? _errorProcessor;
    private INamingPolicy? _namingPolicy;
    private readonly TableDescriptorBuilder _tableDescriptorBuilder = new();
    private TableDescriptorBuilder.MessageTable? _messageTable;

    private readonly IReplicationJsonBMapper _objectDataMapper =
        new ObjectReplicationDataMapper(new ObjectReplicationDataReader());

    private IReplicationJsonBMapper? _jsonDataMapper;
    private JsonSerializerContext? _jsonSerializerContext;


    [UsedImplicitly]
    public OptionsBuilder WithTable(
        Func<TableDescriptorBuilder, TableDescriptorBuilder> builder)
    {
        _messageTable = builder(_tableDescriptorBuilder).Build();
        return this;
    }

    [UsedImplicitly]
    public OptionsBuilder ConnectionString(string connectionString)
    {
        Ensure.Null<NpgsqlConnectionStringBuilder?>(_connectionStringBuilder, nameof(ConnectionString));
        _connectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString);
        return this;
    }

    [UsedImplicitly]
    public OptionsBuilder DataSource(NpgsqlDataSource dataSource)
    {
        Ensure.Null<NpgsqlDataSource?>(_dataSource, nameof(DataSource));
        _dataSource = dataSource;
        return this;
    }

    [UsedImplicitly]
    public OptionsBuilder NamingPolicy(INamingPolicy namingPolicy)
    {
        _namingPolicy = namingPolicy;
        return this;
    }

    [UsedImplicitly]
    public OptionsBuilder JsonContext(JsonSerializerContext jsonSerializerContext)
    {
        _jsonSerializerContext = jsonSerializerContext;
        return this;
    }

    [UsedImplicitly]
    public OptionsBuilder WithPublicationOptions(PublicationManagement.PublicationOptions publicationOptions)
    {

        _publicationOptions =
            publicationOptions with { RegisteredTypes = _publicationOptions.RegisteredTypes };
        return this;
    }

    [UsedImplicitly]
    public OptionsBuilder WithReplicationOptions(
        ReplicationSlotManagement.ReplicationSlotOptions replicationSlotOptions)
    {
        _replicationSlotOptions = replicationSlotOptions;
        return this;
    }

    [UsedImplicitly]
    public OptionsBuilder Consumes<T>(IMessageHandler<T> handler) where T : class
    {
        _typeRegistry.Add(typeof(T), handler);
        return this;
    }

    [UsedImplicitly]
    public OptionsBuilder ConsumesRawObject<T>(IMessageHandler<object> handler) where T : class
        => ConsumesRaw<T>(handler, RawUrnAttribute.RawData.Object, ObjectReplicationDataMapper.Instance);

    [UsedImplicitly]
    public OptionsBuilder ConsumesRawString<T>(IMessageHandler<string> handler) where T : class
        => ConsumesRaw<T>(handler, RawUrnAttribute.RawData.String, StringReplicationDataMapper.Instance);

    [UsedImplicitly]
    public OptionsBuilder ConsumesRawStrings(IMessageHandler<string> handler)
    {
        _replicationDataMapperSelector.Add(WildCard,
            new Tuple<IReplicationJsonBMapper, IMessageHandler>(StringReplicationDataMapper.Instance, handler));
        return this;
    }

    [UsedImplicitly]
    public OptionsBuilder ConsumesRawObjects(IMessageHandler<string> handler)
    {
        _replicationDataMapperSelector.Add(WildCard,
            new Tuple<IReplicationJsonBMapper, IMessageHandler>(ObjectReplicationDataMapper.Instance, handler));
        return this;
    }

    private OptionsBuilder ConsumesRaw<T>(IMessageHandler<string> handler, RawUrnAttribute.RawData filter,
        IReplicationJsonBMapper dataMapper) where T : class
    {
        var urns = typeof(T)
            .GetCustomAttributes(typeof(RawUrnAttribute), false)
            .OfType<RawUrnAttribute>()
            .Where(attribute => attribute.Data == filter)
            .Select(attribute => attribute.Urn).ToList();
        Ensure.NotEmpty<IEnumerable<Uri>>(urns, nameof(NamingPolicy));
        using var urnEnum = urns.GetEnumerator();
        while (urnEnum.MoveNext())
            _replicationDataMapperSelector.Add(urnEnum.Current.ToString(),
                new Tuple<IReplicationJsonBMapper, IMessageHandler>(dataMapper, handler));
        return this;
    }

    [UsedImplicitly]
    public OptionsBuilder WithErrorProcessor(IErrorProcessor? errorProcessor)
    {
        _errorProcessor = errorProcessor;
        return this;
    }

    internal ISubscriberOptions Build()
    {
        _messageTable ??= _tableDescriptorBuilder.Build();
        Ensure.NotNull<NpgsqlConnectionStringBuilder?>(_connectionStringBuilder, $"{nameof(ConnectionString)}");
        Ensure.NotNull<NpgsqlDataSource?>(_dataSource, $"{nameof(DataSource)}");

        if (_typeRegistry.Count > 0)
        {
            Ensure.NotNull<INamingPolicy?>(_namingPolicy, $"{nameof(NamingPolicy)}");
            if (_jsonSerializerContext != null)
            {
                var typeResolver = new JsonTypeResolver(_jsonSerializerContext, _namingPolicy);
                foreach (var type in _typeRegistry.Keys)
                    typeResolver.WhiteList(type);

                _jsonDataMapper =
                    new JsonReplicationDataMapper(typeResolver, new JsonReplicationDataReader(typeResolver));

                foreach (var (key, value) in typeResolver.RegisteredTypes.Join(_typeRegistry, pair => pair.Value,
                             pair => pair.Key, (pair, valuePair) => (pair.Key, valuePair.Value)))
                    _replicationDataMapperSelector.Add(key,
                        new Tuple<IReplicationJsonBMapper, IMessageHandler>(_jsonDataMapper, value));
            }
            else
            {
                throw new ConfigurationException($"`${nameof(Consumes)}<>` requires a valid `{nameof(JsonContext)}`.");
            }
        }

        _publicationOptions = _publicationOptions
            with
            {
                RegisteredTypes = _replicationDataMapperSelector.Keys.Except([WildCard]).ToHashSet(),
                TableDescriptor = _messageTable
            };
        return new SubscriberOptions(
            _dataSource,
            _connectionStringBuilder,
            _publicationOptions,
            _replicationSlotOptions ?? new ReplicationSlotManagement.ReplicationSlotOptions(),
            _errorProcessor ?? new ConsoleOutErrorProcessor(),
            _replicationDataMapperSelector
        );
    }
}

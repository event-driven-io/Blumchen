using System.Text.Json.Serialization;
using Blumchen.Serialization;
using Blumchen.Subscriptions;
using Blumchen.Subscriptions.Management;
using Blumchen.Subscriptions.Replication;
using JetBrains.Annotations;
using Npgsql;

namespace Blumchen.Subscriber;

public sealed partial class OptionsBuilder
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
    private static readonly TableDescriptorBuilder TableDescriptorBuilder = new();
    private TableDescriptorBuilder.MessageTable? _tableDescriptor;

    private readonly IReplicationJsonBMapper _objectDataMapper =
        new ObjectReplicationDataMapper(new ObjectReplicationDataReader());

    private IReplicationJsonBMapper? _jsonDataMapper;


    [UsedImplicitly]
    public OptionsBuilder WithTable(
        Func<TableDescriptorBuilder, TableDescriptorBuilder> builder)
    {
        _tableDescriptor = builder(TableDescriptorBuilder).Build();
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
    public OptionsBuilder ConsumesRawObject<T>(IMessageHandler<object> handler) where T : class
        => ConsumesRaw<T>(handler, ObjectReplicationDataMapper.Instance);

    [UsedImplicitly]
    public OptionsBuilder ConsumesRawString<T>(IMessageHandler<string> handler) where T : class
        => ConsumesRaw<T>(handler, StringReplicationDataMapper.Instance);

    [UsedImplicitly]
    public OptionsBuilder ConsumesRawStrings(IMessageHandler<string> handler)
    {
        Ensure.Empty(_replicationDataMapperSelector, _typeRegistry, nameof(ConsumesRawStrings));

        _replicationDataMapperSelector.Add(WildCard,
            new Tuple<IReplicationJsonBMapper, IMessageHandler>(StringReplicationDataMapper.Instance, handler));
        return this;
    }

    [UsedImplicitly]
    public OptionsBuilder ConsumesRawObjects(IMessageHandler<string> handler)
    {
        Ensure.Empty(_replicationDataMapperSelector, _typeRegistry, nameof(ConsumesRawObjects));

        _replicationDataMapperSelector.Add(WildCard,
            new Tuple<IReplicationJsonBMapper, IMessageHandler>(ObjectReplicationDataMapper.Instance, handler));
        return this;
    }

    private OptionsBuilder ConsumesRaw<T>(IMessageHandler<string> handler,
        IReplicationJsonBMapper dataMapper) where T : class
    {
        var urns = typeof(T)
            .GetCustomAttributes(typeof(RawUrnAttribute), false)
            .OfType<RawUrnAttribute>()
            .Select(attribute => attribute.Urn).ToList();
        Ensure.RawUrn<IEnumerable<Uri>,T>(urns, nameof(NamingPolicy));
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
        _tableDescriptor ??= TableDescriptorBuilder.Build();
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
                throw new ConfigurationException($"`${nameof(Consumes)}<>` requires a valid `{nameof(JsonSerializerContext)}`.");
            }
        }
        Ensure.NotEmpty(_replicationDataMapperSelector, $"{nameof(Consumes)}...");
        _publicationOptions = _publicationOptions
            with
            {
                RegisteredTypes = _replicationDataMapperSelector.Keys.Except([WildCard]).ToHashSet(),
                TableDescriptor = _tableDescriptor
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

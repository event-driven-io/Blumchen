using System.Collections;
using System.Reflection;
using System.Security.Principal;
using System.Text.Json.Serialization;
using Blumchen.Serialization;
using Blumchen.Subscriptions;
using Blumchen.Subscriptions.Management;
using Blumchen.Subscriptions.Replication;
using ImTools;
using JetBrains.Annotations;
using Npgsql;
// ReSharper disable RedundantDefaultMemberInitializer

namespace Blumchen.Subscriber;

public sealed partial class OptionsBuilder
    : IConsumes
{
    internal const string WildCard = "*";

    [System.Diagnostics.CodeAnalysis.NotNull]
    private NpgsqlConnectionStringBuilder? _connectionStringBuilder = default;

    [System.Diagnostics.CodeAnalysis.NotNull]
    private NpgsqlDataSource? _dataSource = default;

    private PublicationManagement.PublicationOptions _publicationOptions = new();
    private ReplicationSlotManagement.ReplicationSlotOptions? _replicationSlotOptions;
    private ImHashMap<Type, Tuple<IMessageHandler, MethodInfo>> _typeRegistry = ImHashMap<Type, Tuple<IMessageHandler, MethodInfo>>.Empty;

    private readonly Dictionary<string, Tuple<IReplicationJsonBMapper, IMessageHandler, MethodInfo>>
        _replicationDataMapperSelector = [];

    private IErrorProcessor? _errorProcessor;
    private static readonly TableDescriptorBuilder TableDescriptorBuilder = new();
    private TableDescriptorBuilder.MessageTable? _tableDescriptor;

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
        => ConsumesRaw<T,object>(handler, ObjectReplicationDataMapper.Instance);

    [UsedImplicitly]
    public OptionsBuilder ConsumesRawString<T>(IMessageHandler<string> handler) where T : class
        => ConsumesRaw<T, string>(handler, StringReplicationDataMapper.Instance);

    [UsedImplicitly]
    public OptionsBuilder ConsumesRawStrings(IMessageHandler<string> handler)
    {
        Ensure.And(
            new EmptyTrait<ICollection>(), _replicationDataMapperSelector,
            new BoolTrait<ImHashMap<Type, Tuple<IMessageHandler, MethodInfo>>>(r => r.IsEmpty, Ensure.CannotBeMixedWithOtherConsumingStrategies),
            _typeRegistry,nameof(ConsumesRawStrings)
            );

        var methodInfo = handler
                             .GetType()
                             .GetMethod(nameof(IMessageHandler<string>.Handle),BindingFlags.Instance | BindingFlags.Public, [typeof(string)])
            ?? throw new ConfigurationException($"Unable to find {nameof(IMessageHandler<string>)} implementation on {handler.GetType().Name}");

        _replicationDataMapperSelector.Add(WildCard,
            new Tuple<IReplicationJsonBMapper, IMessageHandler, MethodInfo>(StringReplicationDataMapper.Instance, handler, methodInfo));
        return this;
    }
    
    [UsedImplicitly]
    public OptionsBuilder ConsumesRawObjects(IMessageHandler<object> handler)
    {
        Ensure.And(
            new EmptyTrait<ICollection>(), _replicationDataMapperSelector,
            new BoolTrait<ImHashMap<Type, Tuple<IMessageHandler, MethodInfo>>>(r => r.IsEmpty, Ensure.CannotBeMixedWithOtherConsumingStrategies),
            _typeRegistry, nameof(ConsumesRawObjects)
        );

        var methodInfo = handler
                             .GetType()
                             .GetMethod(nameof(IMessageHandler<object>.Handle), BindingFlags.Instance | BindingFlags.Public, [typeof(object)])
                         ?? throw new ConfigurationException($"Unable to find {nameof(IMessageHandler<object>)} implementation on {handler.GetType().Name}");


        _replicationDataMapperSelector.Add(WildCard,
            new Tuple<IReplicationJsonBMapper, IMessageHandler, MethodInfo>(ObjectReplicationDataMapper.Instance, handler, methodInfo));
        return this;
    }

    private OptionsBuilder ConsumesRaw<T, TU>(IMessageHandler<TU> handler,
        IReplicationJsonBMapper dataMapper) where T : class where TU : class
    {
        var routingList = typeof(T)
            .GetAttributes<RawRoutedByUrnAttribute>()
            .Union(
                typeof(T).GetAttributes<RawRoutedByStringAttribute>()
                )
            .Select(routed => routed.Route).ToList();
        Ensure.RawUrn<IEnumerable<string>>(routingList, typeof(T).Name);

        var methodInfo = handler
                             .GetType()
                             .GetMethod(nameof(IMessageHandler<TU>.Handle), BindingFlags.Instance | BindingFlags.Public, [typeof(TU)])
                         ?? throw new ConfigurationException($"Unable to find {nameof(IMessageHandler<TU>)} implementation on {handler.GetType().Name}");

        using var urnEnum = routingList.GetEnumerator();
        while (urnEnum.MoveNext())
            _replicationDataMapperSelector.Add(urnEnum.Current,
                new Tuple<IReplicationJsonBMapper, IMessageHandler, MethodInfo>(dataMapper, handler, methodInfo));
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

        if (!_typeRegistry.IsEmpty)
        {
            Ensure.NotNull(_namingPolicy, $"{nameof(NamingPolicy)}");
            if (_jsonSerializerContext != null)
            {
                var typeResolver = new JsonTypeResolver(_jsonSerializerContext, _namingPolicy);
                foreach (var kv in _typeRegistry.Enumerate())
                    typeResolver.WhiteList(kv.Key);

                _jsonDataMapper =
                    new JsonReplicationDataMapper(typeResolver, new JsonReplicationDataReader(typeResolver));

                foreach (var (key, value) in typeResolver.RegisteredTypes.Join(_typeRegistry.Enumerate(), pair => pair.Value,
                             pair => pair.Key, (pair, valuePair) => (pair.Key, valuePair.Value)))
                    _replicationDataMapperSelector.Add(key,
                        new Tuple<IReplicationJsonBMapper, IMessageHandler, MethodInfo>(_jsonDataMapper, value.Item1, value.Item2));
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

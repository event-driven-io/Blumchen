using System.Reflection;
using System.Text.Json.Serialization;
using Blumchen.Serialization;
using Blumchen.Subscriptions.Replication;
using JetBrains.Annotations;

namespace Blumchen.Subscriber;

public sealed partial class OptionsBuilder
{
    private INamingPolicy? _namingPolicy;
    private JsonSerializerContext? _jsonSerializerContext;

    [UsedImplicitly]
    internal OptionsBuilder NamingPolicy(INamingPolicy namingPolicy)
    {
        Ensure.Null(_namingPolicy, nameof(NamingPolicy));
        _namingPolicy = namingPolicy;
        return this;
    }
    
    public interface INamingOptionsContext
    {
        OptionsBuilder AndNamingPolicy(INamingPolicy namingPolicy);
    }

    internal class NamingOptionsContext(OptionsBuilder builder): INamingOptionsContext
    {
        public OptionsBuilder AndNamingPolicy(INamingPolicy namingPolicy)
            => builder.NamingPolicy(namingPolicy);
    }

    public interface IConsumesTypedJsonOptionsContext
    {
        INamingOptionsContext WithJsonContext(JsonSerializerContext jsonSerializerContext);
        IConsumesTypedJsonOptionsContext Consumes<T>(IMessageHandler<T> handler) where T : class;
    }

    internal class ConsumesTypedJsonTypedJsonOptionsContext(OptionsBuilder builder): IConsumesTypedJsonOptionsContext
    {
        public INamingOptionsContext WithJsonContext(JsonSerializerContext jsonSerializerContext)
        {
            builder._jsonSerializerContext = jsonSerializerContext;
            return new NamingOptionsContext(builder);
        }

        public IConsumesTypedJsonOptionsContext Consumes<T>(IMessageHandler<T> handler) where T : class
        {
            return builder.Consumes(handler);
        }
    }

    internal IConsumesTypedJsonOptionsContext Consumes<T>(IMessageHandler<T> handler) where T : class
    {
        Ensure.Empty(_replicationDataMapperSelector, nameof(Consumes));
        var methodInfo = handler
                             .GetType()
                             .GetMethod(nameof(IMessageHandler<T>.Handle), BindingFlags.Instance | BindingFlags.Public, [typeof(T)])
                         ?? throw new ConfigurationException($"Unable to find {nameof(IMessageHandler<T>)} implementation on {handler.GetType().Name}");

        if (_typeRegistry.ContainsKey(typeof(T)))
            throw new ConfigurationException($"`{typeof(T).Name}` was already registered.");
        _typeRegistry.Add(typeof(T), new Tuple<IMessageHandler, MethodInfo>(handler, methodInfo));
        return new ConsumesTypedJsonTypedJsonOptionsContext(this);
    }

    public OptionsBuilder Consumes<T>(IMessageHandler<T> handler, Func<IConsumesTypedJsonOptionsContext, OptionsBuilder> opts) where T : class
        => opts(Consumes(handler));
}

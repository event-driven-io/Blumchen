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
        _namingPolicy = namingPolicy;
        return this;
    }
    
    public interface INamingOptionsContext
    {
        OptionsBuilder NamingPolicy(INamingPolicy namingPolicy);
    }

    internal class NamingOptionsContext(OptionsBuilder builder): INamingOptionsContext
    {
        public OptionsBuilder NamingPolicy(INamingPolicy namingPolicy)
            => builder.NamingPolicy(namingPolicy);
    }

    public interface IConsumesTypedJsonOptionsContext
    {
        INamingOptionsContext JsonContext(JsonSerializerContext jsonSerializerContext);
        IConsumesTypedJsonOptionsContext Consumes<T>(IMessageHandler<T> handler) where T : class;
    }

    internal class ConsumesTypedJsonTypedJsonOptionsContext(OptionsBuilder builder): IConsumesTypedJsonOptionsContext
    {
        public INamingOptionsContext JsonContext(JsonSerializerContext jsonSerializerContext)
        {
            builder._jsonSerializerContext = jsonSerializerContext;
            return new NamingOptionsContext(builder);
        }

        public IConsumesTypedJsonOptionsContext Consumes<T>(IMessageHandler<T> handler) where T : class
        {
            return builder.Consumes<T>(handler);
        }
    }

    public IConsumesTypedJsonOptionsContext Consumes<T>(IMessageHandler<T> handler) where T : class
    {
        Ensure.Empty(_replicationDataMapperSelector, nameof(Consumes));
        var methodInfo = handler
                             .GetType()
                             .GetMethod(nameof(IMessageHandler<T>.Handle), BindingFlags.Instance | BindingFlags.Public, [typeof(T)])
                         ?? throw new ConfigurationException($"Unable to find {nameof(IMessageHandler<T>)} implementation on {handler.GetType().Name}");


        _typeRegistry.Add(typeof(T), new Tuple<IMessageHandler, MethodInfo>(handler, methodInfo));
        return new ConsumesTypedJsonTypedJsonOptionsContext(this);
    }
}

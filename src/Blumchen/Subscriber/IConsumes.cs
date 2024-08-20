using Blumchen.Subscriptions.Replication;
using static Blumchen.Subscriber.OptionsBuilder;

namespace Blumchen.Subscriber;

public interface IConsumes
{
    OptionsBuilder ConsumesRawStrings(IMessageHandler<string> handler);
    OptionsBuilder ConsumesRawObjects(IMessageHandler<object> handler);
    OptionsBuilder ConsumesRawString<T>(IMessageHandler<string> handler) where T : class;
    OptionsBuilder ConsumesRawObject<T>(IMessageHandler<object> handler) where T : class;

    OptionsBuilder Consumes<T>(IMessageHandler<T> handler, Func<IConsumesTypedJsonOptionsContext, OptionsBuilder> opts)
        where T : class;
}

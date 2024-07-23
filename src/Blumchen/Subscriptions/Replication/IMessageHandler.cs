namespace Blumchen.Subscriptions.Replication;

public interface IMessageHandler;

public interface IMessageHandler<in T>: IMessageHandler where T : class
{
    Task Handle(T value);
}
namespace Blumchen.Subscriptions;

public interface IHandler;

public interface IHandler<in T>: IHandler where T : class
{
    Task Handle(T value);
}

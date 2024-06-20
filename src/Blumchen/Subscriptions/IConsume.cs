namespace Blumchen.Subscriptions;
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

public interface IConsume;

public interface IConsumes<in T>: IConsume where T : class
{
    Task Handle(T value);
}

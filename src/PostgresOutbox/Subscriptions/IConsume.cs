namespace PostgresOutbox.Subscriptions;

public interface IConsume;

public interface IConsumes<in T>: IConsume where T : class
{
    Task Handle(T value);
}

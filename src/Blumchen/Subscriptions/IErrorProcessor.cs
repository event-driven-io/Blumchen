namespace Blumchen.Subscriptions;

public interface IErrorProcessor
{
    Func<Exception, Task> Process { get; }
}

public record ConsoleOutErrorProcessor: IErrorProcessor
{
    public Func<Exception, Task> Process => exception => Console.Out.WriteLineAsync($"record id:{0} resulted in error:{exception.Message}");
}

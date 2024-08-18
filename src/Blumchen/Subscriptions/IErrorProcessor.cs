using Microsoft.Extensions.Logging;

namespace Blumchen.Subscriptions;

public interface IErrorProcessor
{
    Func<Exception, string, Task> Process { get; }
}

public record ConsoleOutErrorProcessor: IErrorProcessor
{
    public Func<Exception, string, Task> Process => (exception, id) => Console.Out.WriteLineAsync($"record id:{id} resulted in error:{exception.Message}");
}

public record LoggingErrorProcessor(ILogger<LoggingErrorProcessor> Logger): IErrorProcessor
{
    public Func<Exception, string, Task> Process => (exception, id)
        =>
    {
        Logger.LogError("record id:{id} resulted in error:{exception.Message}", id, exception.Message);
        return Task.CompletedTask;
    };
}

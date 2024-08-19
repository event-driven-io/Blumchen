using Blumchen.Subscriptions.Replication;
using Microsoft.Extensions.Logging;

namespace Blumchen.DependencyInjection;

internal static partial class LoggerExtensions

{
    [LoggerMessage(Message = "{workerName} started", Level = LogLevel.Information)]
    public static partial void ServiceStarted(this ILogger logger, string workerName);

    [LoggerMessage(Message = "{workerName} sopped", Level = LogLevel.Information)]
    public static partial void ServiceStopped(this ILogger logger, string workerName);

    [LoggerMessage(Message = "{message} processed", Level = LogLevel.Trace)]
    public static partial void MessageProcessed(this ILogger logger, IEnvelope message);

}

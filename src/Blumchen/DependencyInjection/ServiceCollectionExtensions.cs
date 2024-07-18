using Blumchen.Subscriptions;
using Blumchen.Subscriptions.Replication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

#pragma warning disable IL2091

namespace Blumchen.DependencyInjection;

public static class ServiceCollectionExtensions
{

    public static IServiceCollection AddBlumchen<T>(
        this IServiceCollection service,
        Func<IServiceProvider, IWorkerOptionsBuilder, IWorkerOptionsBuilder> workerOptions)
        where T : class, IMessageHandler =>
        service
            .AddKeyedSingleton(typeof(T), (provider, _) => workerOptions(provider, new WorkerOptionsBuilder()).Build())
            .AddHostedService(provider =>
                new Worker<T>(workerOptions(provider, new WorkerOptionsBuilder()).Build(),
                    provider.GetRequiredService<ILogger<Worker<T>>>()));


}

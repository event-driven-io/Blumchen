using Blumchen.Subscriptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;

#pragma warning disable IL2091

namespace Blumchen.Workers;

public static class ServiceCollectionExtensions
{

    public static IServiceCollection AddBlumchen<T>(
        this IServiceCollection service,
        Func<IServiceProvider, IWorkerOptionsBuilder, IWorkerOptionsBuilder> workerOptions)
        where T : class, IHandler =>
        service
            .AddKeyedSingleton(typeof(T), (provider, _) => workerOptions(provider, new WorkerOptionsBuilder()).Build())
            .AddHostedService(provider =>
                new Worker<T>(workerOptions(provider, new WorkerOptionsBuilder()).Build(),
                    provider.GetRequiredService<ILogger<Worker<T>>>()));


}

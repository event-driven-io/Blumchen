using Blumchen.Subscriptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

#pragma warning disable IL2091

namespace Blumchen.DependencyInjection;

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
                    ServiceProviderServiceExtensions.GetRequiredService<ILogger<Worker<T>>>(provider)));


}

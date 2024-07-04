using Microsoft.Extensions.DependencyInjection;

namespace Blumchen.Workers;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBlumchen<T,TU>(this IServiceCollection service, T? instance = default)
        where T : Worker<TU> where TU : class =>
        instance is null
            ? service.AddHostedService<T>()
            : service.AddHostedService(_=>instance);
}

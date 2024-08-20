using Blumchen.Subscriber;
using Blumchen.Subscriptions.Replication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

#pragma warning disable IL2091

namespace Blumchen.DependencyInjection;

public static class ServiceCollectionExtensions
{

    public static IServiceCollection AddBlumchen<T>(
        this IServiceCollection service,
        Func<IServiceProvider, IWorkerOptionsBuilder, IWorkerOptionsBuilder> workerOptions)
        where T : class, IMessageHandler =>
        service
            .AddHostedService(provider =>
                new Worker<T>(workerOptions(provider, new WorkerOptionsBuilder()).Build(),
                    provider.GetRequiredService<ILogger<Worker<T>>>()));

    public static IServiceCollection AddBlumchen<T>(
        this IServiceCollection service,
        string connectionString,
        Func<IServiceProvider, IConsumes, OptionsBuilder> consumerFn) where T : class, IMessageHandler {
        return service
            .AddHostedService(provider =>
                new Worker<T>(MinimalWorkerOptions(provider, new WorkerOptionsBuilder()).Build(),
                provider.GetService<ILogger<Worker<T>>>() ?? new NullLogger<Worker<T>>()));

        IWorkerOptionsBuilder MinimalWorkerOptions(IServiceProvider provider, IWorkerOptionsBuilder builder)
             => builder.Subscription(optionsBuilder => consumerFn(provider, optionsBuilder)
                .ConnectionString(connectionString)
                .DataSource(new NpgsqlDataSourceBuilder(connectionString)
                    .UseLoggerFactory(provider.GetService<ILoggerFactory>()).Build()));

        
    }
}

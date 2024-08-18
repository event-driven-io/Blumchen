using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Tests;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddXunitLogging(this IServiceCollection services, ITestOutputHelper output) =>
        services
            .AddLogging(loggingBuilder =>
            {
                loggingBuilder
                    .AddFilter("Microsoft", LogLevel.Warning)
                    .AddFilter("System", LogLevel.Warning)
                    .AddFilter("Npgsql", LogLevel.Information)
                    .AddFilter("Blumchen", LogLevel.Trace)
                    .AddFilter("SubscriberWorker", LogLevel.Debug)
                    .AddXunit(output);
            });
}

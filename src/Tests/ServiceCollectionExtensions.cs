using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Tests;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddXunitLogging(this IServiceCollection services, ITestOutputHelper output)
        => services.AddLogging(loggingBuilder =>
            loggingBuilder
                .AddFilter("Tests", LogLevel.Trace)
                .AddXunit(output)
            );
}

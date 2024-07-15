using System.Text.Json.Serialization;
using Blumchen.Serialization;
using Blumchen.Subscriptions;
using Blumchen.Workers;
using Commons;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly.Retry;
using Polly;
using SubscriberWorker;
using Npgsql;


#pragma warning disable CS8601 // Possible null reference assignment.
Console.Title = typeof(Program).Assembly.GetName().Name;
#pragma warning restore CS8601 // Possible null reference assignment.



AppDomain.CurrentDomain.UnhandledException += (_, e) => Console.Out.WriteLine(e.ExceptionObject.ToString());
TaskScheduler.UnobservedTaskException += (_, e) => Console.Out.WriteLine(e.Exception.ToString());

var cancellationTokenSource = new CancellationTokenSource();
var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddBlumchen<SubscriberWorker<UserCreatedContract>, UserCreatedContract>()
    .AddSingleton<IHandler<UserCreatedContract>, Handler<UserCreatedContract>>()
    .AddBlumchen<SubscriberWorker<UserDeletedContract>, UserDeletedContract>()
    .AddSingleton<IHandler<UserDeletedContract>, Handler<UserDeletedContract>>()
    .AddSingleton(Settings.ConnectionString)
    .AddTransient(sp =>
        new NpgsqlDataSourceBuilder(Settings.ConnectionString)
            .UseLoggerFactory(sp.GetRequiredService<ILoggerFactory>()).Build())
    .AddSingleton<INamingPolicy, AttributeNamingPolicy>()
    .AddSingleton<IErrorProcessor, ConsoleOutErrorProcessor>()
    .AddSingleton<JsonSerializerContext, SourceGenerationContext>()
    .AddResiliencePipeline("default", (pipelineBuilder, _) =>
        pipelineBuilder
            .AddRetry(new RetryStrategyOptions
            {
                BackoffType = DelayBackoffType.Constant,
                Delay = TimeSpan.FromSeconds(5),
                MaxRetryAttempts = int.MaxValue
            }).Build())
    .AddLogging(loggingBuilder =>
    {
        loggingBuilder
            .AddFilter("Microsoft", LogLevel.Warning)
            .AddFilter("System", LogLevel.Warning)
            .AddFilter("Npgsql", LogLevel.Information)
            .AddFilter("Blumchen", LogLevel.Debug)
            .AddFilter("SubscriberWorker", LogLevel.Debug)
            .AddSimpleConsole();
    }).AddSingleton<ILogger>(sp => sp.GetRequiredService<ILoggerFactory>().CreateLogger<ILogger>());

await builder
    .Build()
    .RunAsync(cancellationTokenSource.Token)
    .ConfigureAwait(false);

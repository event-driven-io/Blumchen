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

    .AddSingleton<INamingPolicy, AttributeNamingPolicy>()
    .AddSingleton<IErrorProcessor, ConsoleOutErrorProcessor>()
    .AddSingleton<JsonSerializerContext, SourceGenerationContext>()
    .AddSingleton(new DbOptions(Settings.ConnectionString))
    .AddResiliencePipeline("default",(pipelineBuilder,context) =>
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
            .AddConsole();
    });

await builder
    .Build()
    .RunAsync(cancellationTokenSource.Token)
    .ConfigureAwait(false);

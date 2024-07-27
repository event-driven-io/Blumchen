using System.Text.Json.Serialization;
using Blumchen.DependencyInjection;
using Blumchen.Serialization;
using Blumchen.Subscriptions;
using Commons;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly.Retry;
using Polly;
using SubscriberWorker;
using Npgsql;
using Blumchen.Subscriptions.Management;
using Blumchen.Subscriptions.Replication;
using Polly.Registry;

#pragma warning disable CS8601 // Possible null reference assignment.
Console.Title = typeof(Program).Assembly.GetName().Name;
#pragma warning restore CS8601 // Possible null reference assignment.

AppDomain.CurrentDomain.UnhandledException += (_, e) => Console.Out.WriteLine(e.ExceptionObject.ToString());
TaskScheduler.UnobservedTaskException += (_, e) => Console.Out.WriteLine(e.Exception.ToString());

var cancellationTokenSource = new CancellationTokenSource();
var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddSingleton<IMessageHandler<UserCreatedContract>, HandleImpl1>()
    .AddSingleton<IMessageHandler<UserModifiedContract>, HandleImpl1>()
    .AddSingleton<IMessageHandler<UserDeletedContract>, HandleImpl2>()

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
    })
    .AddTransient(sp =>
        new NpgsqlDataSourceBuilder(Settings.ConnectionString)
            .UseLoggerFactory(sp.GetRequiredService<ILoggerFactory>()).Build())

    .AddBlumchen<HandleImpl1>((provider, workerOptions) =>
        workerOptions
            .Subscription(subscriptionOptions =>
                subscriptionOptions
                    .ConnectionString(Settings.ConnectionString)
                    .DataSource(provider.GetRequiredService<NpgsqlDataSource>())
                    .WithReplicationOptions(new ReplicationSlotManagement.ReplicationSlotOptions($"{nameof(HandleImpl1)}_slot"))
                    .WithPublicationOptions(new PublicationManagement.PublicationOptions($"{nameof(HandleImpl1)}_pub"))
                    .WithErrorProcessor(provider.GetRequiredService<IErrorProcessor>())
                    
                    .Consumes(provider.GetRequiredService<IMessageHandler<UserCreatedContract>>())
                    .Consumes(provider.GetRequiredService<IMessageHandler<UserModifiedContract>>())
                    .JsonContext(SourceGenerationContext.Default)
                    .NamingPolicy(provider.GetRequiredService<INamingPolicy>())
                )
            .ResiliencyPipeline(provider.GetRequiredService<ResiliencePipelineProvider<string>>().GetPipeline("default"))
    )
    .AddBlumchen<HandleImpl2>((provider, workerOptions) =>
        workerOptions
            .Subscription(subscriptionOptions =>
                subscriptionOptions.ConnectionString(Settings.ConnectionString)
                    .DataSource(provider.GetRequiredService<NpgsqlDataSource>())
                    .WithReplicationOptions(new ReplicationSlotManagement.ReplicationSlotOptions($"{nameof(HandleImpl2)}_slot"))
                    .WithPublicationOptions(new PublicationManagement.PublicationOptions($"{nameof(HandleImpl2)}_pub"))
                    .WithErrorProcessor(provider.GetRequiredService<IErrorProcessor>())
                    
                    .Consumes(provider.GetRequiredService<IMessageHandler<UserDeletedContract>>())
                    .JsonContext(SourceGenerationContext.Default)
                    .NamingPolicy(provider.GetRequiredService<INamingPolicy>())
                )
            .ResiliencyPipeline(provider.GetRequiredService<ResiliencePipelineProvider<string>>().GetPipeline("default"))
        );

await builder
    .Build()
    .RunAsync(cancellationTokenSource.Token)
    .ConfigureAwait(false);

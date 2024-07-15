using System.Text.Json.Serialization;
using Blumchen.Serialization;
using Blumchen.Subscriptions;
using Blumchen.Subscriptions.Management;
using Blumchen.Workers;
using Microsoft.Extensions.Logging;
using Npgsql;
using Polly.Registry;
// ReSharper disable ClassNeverInstantiated.Global

namespace SubscriberWorker;
public class SubscriberWorker<T>(
    NpgsqlDataSource dataSource,
    string connectionString,
    IHandler<T> handler,
    JsonSerializerContext jsonSerializerContext,
    ResiliencePipelineProvider<string> pipelineProvider,
    INamingPolicy namingPolicy,
    IErrorProcessor errorProcessor,
    ILogger logger
): Worker<T>(dataSource
    , connectionString
    , handler
    , jsonSerializerContext
    , errorProcessor
    , pipelineProvider.GetPipeline("default")
    , namingPolicy
    , new PublicationManagement.PublicationSetupOptions($"{typeof(T).Name}_pub")
    , new ReplicationSlotManagement.ReplicationSlotSetupOptions($"{typeof(T).Name}_slot")
    , tableDescriptorBuilder => tableDescriptorBuilder.UseDefaults()
    , logger) where T : class;

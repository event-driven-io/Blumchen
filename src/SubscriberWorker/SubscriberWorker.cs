using System.Text.Json.Serialization;
using Blumchen.Serialization;
using Blumchen.Subscriptions;
using Blumchen.Subscriptions.Management;
using Blumchen.Workers;
using Microsoft.Extensions.Logging;
using Polly.Registry;
// ReSharper disable ClassNeverInstantiated.Global

namespace SubscriberWorker;
public class SubscriberWorker<T>(
    DbOptions dbOptions,
    IHandler<T> handler,
    JsonSerializerContext jsonSerializerContext,
    ResiliencePipelineProvider<string> pipelineProvider,
    INamingPolicy namingPolicy,
    IErrorProcessor errorProcessor,
    ILoggerFactory loggerFactory
): Worker<T>(dbOptions
    , handler
    , jsonSerializerContext
    , errorProcessor
    , pipelineProvider.GetPipeline("default")
    , namingPolicy
    , new PublicationManagement.PublicationSetupOptions($"{typeof(T).Name}_pub")
    , new ReplicationSlotManagement.ReplicationSlotSetupOptions($"{typeof(T).Name}_slot")
    , loggerFactory) where T : class;

using System.Diagnostics.CodeAnalysis;
using Blumchen.Subscriber;
using Blumchen.Subscriptions.Management;
using Npgsql;
using Npgsql.Replication;
using Polly;

namespace Blumchen.DependencyInjection;

public record WorkerOptions(
    ISubscriberOptions SubscriberOptions,
    ResiliencePipeline OuterPipeline,
    ResiliencePipeline InnerPipeline);

public interface IWorkerOptionsBuilder
{
    IWorkerOptionsBuilder ResiliencyPipeline(ResiliencePipeline resiliencePipeline);
    IWorkerOptionsBuilder Subscription(Func<OptionsBuilder, OptionsBuilder>? builder);
    WorkerOptions Build();
    IWorkerOptionsBuilder EnableSubscriptionAutoHeal();
}

internal sealed class WorkerOptionsBuilder: IWorkerOptionsBuilder
{
    private ResiliencePipeline? _outerPipeline;
    private Func<string, string, ResiliencePipeline>? _innerPipelineFn;
    private Func<OptionsBuilder, OptionsBuilder>? _builder;

    public IWorkerOptionsBuilder ResiliencyPipeline(ResiliencePipeline resiliencePipeline)
    {
        _outerPipeline = resiliencePipeline;
        return this;
    }public IWorkerOptionsBuilder Subscription(Func<OptionsBuilder, OptionsBuilder>? builder)
    {
        _builder = builder;
        return this; 
    }

    public WorkerOptions Build()
    {
        ArgumentNullException.ThrowIfNull(_builder);
        var subscriberOptions = _builder(new OptionsBuilder()).Build();
        return new(subscriberOptions, _outerPipeline ?? ResiliencePipeline.Empty,
            _innerPipelineFn?.Invoke(subscriberOptions.ReplicationOptions.SlotName,subscriberOptions.ConnectionStringBuilder.ConnectionString) ??
            ResiliencePipeline.Empty
        );
    }

    public IWorkerOptionsBuilder EnableSubscriptionAutoHeal()
    {
        _innerPipelineFn = (replicationSlotName, connectionString) => new ResiliencePipelineBuilder().AddRetry(new()
        {
            ShouldHandle =
                new PredicateBuilder().Handle<PostgresException>(exception =>
                    exception.SqlState.Equals("55000", StringComparison.OrdinalIgnoreCase)),
            MaxRetryAttempts = int.MaxValue,
            OnRetry = async args =>
            {
                await using var conn = new LogicalReplicationConnection(connectionString);
                await conn.Open(args.Context.CancellationToken);
                await conn.ReCreate(replicationSlotName, args.Context.CancellationToken).ConfigureAwait(false);
            },
        }).Build();
        return this;
    }
}


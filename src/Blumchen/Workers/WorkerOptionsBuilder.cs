using Blumchen.Subscriptions;
using Polly;

namespace Blumchen.Workers;

public record WorkerOptions(ResiliencePipeline ResiliencePipeline, ISubscriptionOptions SubscriptionOptions);

public interface IWorkerOptionsBuilder
{
    IWorkerOptionsBuilder ResiliencyPipeline(ResiliencePipeline resiliencePipeline);
    IWorkerOptionsBuilder Subscription(Func<SubscriptionOptionsBuilder, SubscriptionOptionsBuilder>? builder);
    WorkerOptions Build();
}

internal sealed class WorkerOptionsBuilder: IWorkerOptionsBuilder
{
    private ResiliencePipeline? _resiliencePipeline = default;
    private Func<SubscriptionOptionsBuilder, SubscriptionOptionsBuilder>? _builder;

    public IWorkerOptionsBuilder ResiliencyPipeline(ResiliencePipeline resiliencePipeline)
    {
        _resiliencePipeline = resiliencePipeline;
        return this;
    }public IWorkerOptionsBuilder Subscription(Func<SubscriptionOptionsBuilder, SubscriptionOptionsBuilder>? builder)
    {
        _builder = builder;
        return this;
    }

    public WorkerOptions Build()
    {
        ArgumentNullException.ThrowIfNull(_resiliencePipeline);
        ArgumentNullException.ThrowIfNull(_builder);
        return new(_resiliencePipeline, _builder(new SubscriptionOptionsBuilder()).Build());
    }
}


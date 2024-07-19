using Blumchen.Subscriber;
using Polly;

namespace Blumchen.DependencyInjection;

public record WorkerOptions(ResiliencePipeline ResiliencePipeline, ISubscriberOptions SubscriberOptions);

public interface IWorkerOptionsBuilder
{
    IWorkerOptionsBuilder ResiliencyPipeline(ResiliencePipeline resiliencePipeline);
    IWorkerOptionsBuilder Subscription(Func<OptionsBuilder, OptionsBuilder>? builder);
    WorkerOptions Build();
}

internal sealed class WorkerOptionsBuilder: IWorkerOptionsBuilder
{
    private ResiliencePipeline? _resiliencePipeline = default;
    private Func<OptionsBuilder, OptionsBuilder>? _builder;

    public IWorkerOptionsBuilder ResiliencyPipeline(ResiliencePipeline resiliencePipeline)
    {
        _resiliencePipeline = resiliencePipeline;
        return this;
    }public IWorkerOptionsBuilder Subscription(Func<OptionsBuilder, OptionsBuilder>? builder)
    {
        _builder = builder;
        return this;
    }

    public WorkerOptions Build()
    {
        ArgumentNullException.ThrowIfNull(_resiliencePipeline);
        ArgumentNullException.ThrowIfNull(_builder);
        return new(_resiliencePipeline, _builder(new OptionsBuilder()).Build());
    }
}


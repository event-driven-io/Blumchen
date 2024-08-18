using Blumchen.DependencyInjection;
using Blumchen.Publisher;
using Blumchen.Serialization;
using Blumchen.Subscriber;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit.Abstractions;
using SubscriberOptionsBuilder = Blumchen.Subscriber.OptionsBuilder;
using PublisherOptionsBuilder = Blumchen.Publisher.OptionsBuilder;

namespace Tests
{
    public class ServiceWorker(ITestOutputHelper testOutputHelper): DatabaseFixture(testOutputHelper)
    {
        [Fact]
        public async Task ConsumesRawStrings() => await Consumes<string>(
            (services, opts) => opts.ConsumesRawStrings(services.GetRequiredService<TestHandler<string>>())
            );

        [Fact]
        public async Task ConsumesRawObjects() =>
            await Consumes<object>(
            (services,opts) => opts.ConsumesRawObjects(services.GetRequiredService<TestHandler<object>>())
            );

        [Fact]
        public async Task ConsumesRawString() => await Consumes<string>(
            (services, opts) => opts.ConsumesRawString<DecoratedContract>(services.GetRequiredService<TestHandler<string>>())
        );

        [Fact]
        public async Task ConsumesRawObject() => await Consumes<object>(
                (services, opts) =>
                {
                    var handler = services.GetRequiredService<TestHandler<object>>();
                    return opts.ConsumesRawObject<DecoratedContract>(handler);
                });

        

        [Fact]
        public async Task ConsumesJson_without_shared_kernel() => await Consumes<SubscriberUserCreated>(
            (services, builder) => builder
                .Consumes(services.GetRequiredService<TestHandler<SubscriberUserCreated>>(),
                    opts => opts
                        .WithJsonContext(SubscriberContext.Default)
                        .AndNamingPolicy(new AttributeNamingPolicy())
                    )
        );

        [Fact]
        public async Task ConsumesJson_with_shared_kernel()
        {
            var namingPolicy = new FQNNamingPolicy();
            await Consumes<PublisherUserCreated>(
                (services, builder) => builder
                    .Consumes(services.GetRequiredService<TestHandler<PublisherUserCreated>>(),
                        opts => opts
                            .WithJsonContext(PublisherContext.Default)
                            .AndNamingPolicy(namingPolicy)
                    ), namingPolicy
            );
        }

        private async Task Consumes<T>(
            Func<IServiceProvider,
            IConsumes,SubscriberOptionsBuilder> consumesFn,
            INamingPolicy? namingPolicy = default
        ) where T : class
        {
            var ct = new CancellationTokenSource(TimeSpan.FromSeconds(3));

            var options = await new PublisherOptionsBuilder()
                .JsonContext(PublisherContext.Default)
                .NamingPolicy(namingPolicy ?? new AttributeNamingPolicy())
                .Build()
                .EnsureTable(Container.GetConnectionString(), ct.Token);

            await MessageAppender.AppendAsync(
                new PublisherUserCreated(Guid.NewGuid(), nameof(PublisherUserCreated)),
                options,
                Container.GetConnectionString(),
                ct.Token
            );
            
            var builder = Host.CreateApplicationBuilder();
            builder.Services
                .AddXunitLogging(Output)
                .AddSingleton<TestHandler<T>>()
                .AddBlumchen<TestMessageHandler<T>>(
                    Container.GetConnectionString(),
                    consumesFn
                    );

            await builder
                .Build()
                .RunAsync(ct.Token);

        }
    }
}

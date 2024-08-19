using Blumchen;
using Blumchen.Serialization;
using Blumchen.Subscriber;
using Blumchen.Subscriptions;
using Blumchen.Subscriptions.Replication;
using Npgsql;
using NSubstitute;
using static Blumchen.Subscriptions.Subscription;

namespace UnitTests
{
    public class subscriber_options_builder
    {
        private const string ValidConnectionString = 
            "PORT = 5432; HOST = 127.0.0.1; TIMEOUT = 15; MINPOOLSIZE = 1; MAXPOOLSIZE = 100; COMMANDTIMEOUT = 20; Include Error Detail=True; DATABASE = 'postgres'; PASSWORD = 'postgres'; USER ID = 'postgres';";
        private readonly Func<string, OptionsBuilder> _builder = c => new OptionsBuilder().ConnectionString(c).DataSource(new NpgsqlDataSourceBuilder(c).Build());

        [Fact]
        public void requires_at_least_one_method_call_to_connectionstring()
        {
            var exception = Record.Exception(() => new OptionsBuilder().Build());
            Assert.IsType<ConfigurationException>(exception);
            Assert.Equal("`ConnectionString` method not called on OptionsBuilder", exception.Message);
        }

        [Fact]
        public void requires_at_most_one_method_call_to_connectionstring()
        {
            var exception = Record.Exception(() => new OptionsBuilder()
                .ConnectionString(ValidConnectionString)
                .ConnectionString(ValidConnectionString)
                .Build());
            Assert.IsType<ConfigurationException>(exception);
            Assert.Equal("`ConnectionString` method on OptionsBuilder called more then once", exception.Message);
        }

        [Fact]
        public void requires_at_least_one_method_call_to_datasource()
        {
            var exception = Record.Exception(() => new OptionsBuilder().ConnectionString(ValidConnectionString).Build());
            Assert.IsType<ConfigurationException>(exception);
            Assert.Equal("`DataSource` method not called on OptionsBuilder", exception.Message);
        }

        [Fact]
        public void requires_at_most_one_method_call_to_datasource()
        {
            var exception = Record.Exception(() => _builder(ValidConnectionString).DataSource(new NpgsqlDataSourceBuilder(ValidConnectionString).Build()).Build());
            Assert.IsType<ConfigurationException>(exception);
            Assert.Equal("`DataSource` method on OptionsBuilder called more then once", exception.Message);
        }


        [Fact]
        public void requires_at_least_one_method_call_to_consumes()
        {
            var exception = Record.Exception(() => _builder(ValidConnectionString).Build());
            Assert.IsType<ConfigurationException>(exception);
            Assert.Equal("No `Consumes...` method called on OptionsBuilder", exception.Message);
        }

        [Fact]
        public void has_default_options()
        {
            var messageHandler = Substitute.For<IMessageHandler<string>>();
            var opts = _builder(ValidConnectionString).ConsumesRawStrings(messageHandler).Build();

            Assert.NotNull(opts.PublicationOptions);
            Assert.Equal(CreateStyle.WhenNotExists, opts.PublicationOptions.CreateStyle);
            Assert.False(opts.PublicationOptions.ShouldReAddTablesIfWereRecreated);
            Assert.Empty(opts.PublicationOptions.RegisteredTypes);
            Assert.Equal(opts.PublicationOptions.PublicationName, opts.PublicationOptions.PublicationName);
            Assert.Equal(new TableDescriptorBuilder().Build(), opts.PublicationOptions.TableDescriptor);

            Assert.NotNull(opts.ReplicationOptions);
            Assert.Equal($"{TableDescriptorBuilder.MessageTable.DefaultName}_slot", opts.ReplicationOptions.SlotName);
            Assert.Equal(CreateStyle.WhenNotExists, opts.ReplicationOptions.CreateStyle);
            Assert.False(opts.ReplicationOptions.Binary);

            Assert.IsType<ConsoleOutErrorProcessor>(opts.ErrorProcessor);
        }

        [Fact]
        public void with_ConsumesRawStrings()
        {
            var messageHandler = Substitute.For<IMessageHandler<string>>();
            var opts = _builder(ValidConnectionString).ConsumesRawStrings(messageHandler).Build();
            Assert.Equivalent(new Dictionary<string, Tuple<IReplicationJsonBMapper, IMessageHandler>> { { OptionsBuilder.WildCard, new Tuple<IReplicationJsonBMapper, IMessageHandler>(StringReplicationDataMapper.Instance, messageHandler) } }, opts.Registry);
        }

        [Fact]
        public void with_ConsumesRawObjects()
        {
            var messageHandler = Substitute.For<IMessageHandler<object>>();
            var opts = _builder(ValidConnectionString).ConsumesRawObjects(messageHandler).Build();
            Assert.Equivalent(new Dictionary<string, Tuple<IReplicationJsonBMapper, IMessageHandler>> { { OptionsBuilder.WildCard, new Tuple<IReplicationJsonBMapper, IMessageHandler>(ObjectReplicationDataMapper.Instance, messageHandler) } }, opts.Registry);
        }

        [Fact]
        public void ConsumesRawObjects_cannot_be_mixed_with_other_consuming_strategies()
        {
            var messageHandler1 = Substitute.For<IMessageHandler<object>>();
            var messageHandler2 = Substitute.For<IMessageHandler<object>>();
            var exception = Record.Exception(() =>
                _builder(ValidConnectionString).ConsumesRawStrings(messageHandler2).ConsumesRawObjects(messageHandler1)
                    .Build());
            Assert.IsType<ConfigurationException>(exception);
            Assert.Equal("`ConsumesRawObjects` cannot be mixed with other consuming strategies", exception.Message);
        }

        [Fact]
        public void ConsumesRawStrings_cannot_be_mixed_with_other_consuming_strategies()
        {
            var messageHandler1 = Substitute.For<IMessageHandler<object>>();
            var messageHandler2 = Substitute.For<IMessageHandler<object>>();
            var exception = Record.Exception(() =>
                _builder(ValidConnectionString).ConsumesRawObjects(messageHandler1).ConsumesRawStrings(messageHandler2)
                    .Build());
            Assert.IsType<ConfigurationException>(exception);
            Assert.Equal("`ConsumesRawStrings` cannot be mixed with other consuming strategies", exception.Message);
        }

        [Fact]
        public void with_typed_raw_consumer_of_object_requires_RawUrn_decoration()
        {
            var messageHandler = Substitute.For<IMessageHandler<object>>();
            var exception = Record.Exception(() => _builder(ValidConnectionString).ConsumesRawObject<InvalidMessage>(messageHandler).Build());
            Assert.IsType<ConfigurationException>(exception);
            Assert.Equal($"`{nameof(RawRoutedByUrnAttribute)}` missing on `InvalidMessage` message type", exception.Message);
        }

        [Fact]
        public void with_typed_raw_consumer_of_string_requires_RawUrn_decoration()
        {
            var messageHandler = Substitute.For<IMessageHandler<string>>();
            var exception = Record.Exception(() => _builder(ValidConnectionString).ConsumesRawString<InvalidMessage>(messageHandler).Build());
            Assert.IsType<ConfigurationException>(exception);
            Assert.Equal($"`{nameof(RawRoutedByUrnAttribute)}` missing on `InvalidMessage` message type", exception.Message);
        }

        [Fact]
        public void does_not_allow_multiple_registration_of_the_same_typed_consumer()
        {
            var messageHandler = Substitute.For<IMessageHandler<UserCreatedContract>>();
            var exception = Record.Exception(() => _builder(ValidConnectionString)
                .Consumes(messageHandler)
                .WithJsonContext(SourceGenerationContext.Default)
                .AndNamingPolicy(new AttributeNamingPolicy())
                .Consumes(messageHandler)
                .WithJsonContext(SourceGenerationContext.Default)
                .AndNamingPolicy(new AttributeNamingPolicy())
                .Build());
            Assert.IsType<ConfigurationException>(exception);
            Assert.Equal("`UserCreatedContract` was already registered.", exception.Message);
        }

        [Fact]
        public void with_typed_consumer_allows_only_one_naming_policy_instance()
        {
            var userCreatedMessageHandler = Substitute.For<IMessageHandler<UserCreatedContract>>();
            var userRegisteredMessageHandler = Substitute.For<IMessageHandler<UserRegisteredContract>>();
            var exception = Record.Exception(() => _builder(ValidConnectionString)
                .Consumes(userCreatedMessageHandler)
                .WithJsonContext(SourceGenerationContext.Default)
                .AndNamingPolicy(new AttributeNamingPolicy())
                .Consumes(userRegisteredMessageHandler)
                .WithJsonContext(SourceGenerationContext.Default)
                .AndNamingPolicy(new AttributeNamingPolicy())
                .Build());
            Assert.IsType<ConfigurationException>(exception);
            Assert.Equal("`NamingPolicy` method on OptionsBuilder called more then once", exception.Message);
        }
    }
}

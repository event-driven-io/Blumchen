using Blumchen;
using Blumchen.Subscriber;
using Blumchen.Subscriptions;
using Blumchen.Subscriptions.Replication;
using Npgsql;
using NSubstitute;
using static Blumchen.Subscriptions.Management.PublicationManagement;
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
        public void with_typed_raw_consumer_of_object_requires_RawUrn_decoration()
        {
            var messageHandler = Substitute.For<IMessageHandler<object>>();
            var exception = Record.Exception(() => _builder(ValidConnectionString).ConsumesRawObject<InvalidMessage>(messageHandler).Build());
            Assert.IsType<ConfigurationException>(exception);
            Assert.Equal("No `NamingPolicy` method called on OptionsBuilder", exception.Message);
        }

        [Fact]
        public void with_typed_raw_consumer_of_string_requires_RawUrn_decoration()
        {
            var messageHandler = Substitute.For<IMessageHandler<string>>();
            var exception = Record.Exception(() => _builder(ValidConnectionString).ConsumesRawString<InvalidMessage>(messageHandler).Build());
            Assert.IsType<ConfigurationException>(exception);
            Assert.Equal("No `NamingPolicy` method called on OptionsBuilder", exception.Message);
        }
    }
}

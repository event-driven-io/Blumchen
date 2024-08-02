using Blumchen;
using Blumchen.Publisher;
using Blumchen.Serialization;

namespace UnitTests
{
    public class publisher_options_builder
    {

        [Fact]
        public void requires_a_method_call_to_JsonContext()
        {
            var exception = Record.Exception(() => new OptionsBuilder().Build());
            Assert.IsType<ConfigurationException>(exception);
            Assert.Equal("`JsonContext` method not called on OptionsBuilder", exception.Message);
        }

        [Fact]
        public void requires_a_method_call_to_NamingPolicy()
        {
            var exception = Record.Exception(() =>
                new OptionsBuilder().JsonContext(SourceGenerationContext.Default).Build());
            Assert.IsType<ConfigurationException>(exception);
            Assert.Equal("`NamingPolicy` method not called on OptionsBuilder", exception.Message);
        }

        [Fact]
        public void has_default_options()
        {
            var opts = new OptionsBuilder().JsonContext(SourceGenerationContext.Default)
                .NamingPolicy(new AttributeNamingPolicy()).Build();

            Assert.NotNull(opts.JsonTypeResolver);
            Assert.Equal(new TableDescriptorBuilder().Build(), opts.TableDescriptor);
        }
    }
}

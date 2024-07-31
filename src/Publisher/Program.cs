using Blumchen.Database;
using Blumchen.Publisher;
using Blumchen.Serialization;
using CommandLine;
using CommandLine.Text;
using Commons;
using Microsoft.Extensions.Logging;
using Npgsql;
using Publisher;
using UserCreated = Publisher.UserCreated;
using UserDeleted = Publisher.UserDeleted;
using UserModified = Publisher.UserModified;
using UserSubscribed = Publisher.UserSubscribed;

Console.Title = typeof(Program).Assembly.GetName().Name!;

var generator = Options.Generator;

var cts = new CancellationTokenSource();

var resolver = await new OptionsBuilder()
    .JsonContext(SourceGenerationContext.Default)
    .NamingPolicy(new AttributeNamingPolicy())
    .WithTable(builder => builder.UseDefaults()) //default, but explicit
    .Build()
    .EnsureTable(Settings.ConnectionString, cts.Token)//enforce table existence and conformity - db roundtrip
    .ConfigureAwait(false);

//Or you might want to verify at a later stage
await new NpgsqlDataSourceBuilder(Settings.ConnectionString)
    .UseLoggerFactory(LoggerFactory.Create(builder => builder.AddConsole()))
    .Build()
    .EnsureTableExists(resolver.TableDescriptor, cts.Token).ConfigureAwait(false);
Parser.Default.ParseArguments<Options>("--help".Split());
do
{
    async void GenerateFn(Options o) => await Generate(generator.Join(o.MessageTypes, pair => pair.Key, s => s, (pair, s) => pair).ToDictionary(), o.Count, resolver, cts);

    Parser.Default.ParseArguments<Options>(Console.ReadLine()?.Split())
        .WithParsed(GenerateFn)
        .WithNotParsed(HandleParseError);

} while (true);

static void HandleParseError(IEnumerable<Error> errs) => Console.WriteLine("Errors:" + string.Join(',', errs.Select(e => e.Tag)));

async Task Generate(Dictionary<string, Func<object>> dictionary, int result, PublisherOptions publisherOptions,
    CancellationTokenSource cancellationTokenSource)
{
    var generatorLength = dictionary.Count;
    var messageCount = result / generatorLength;
    var ct = cancellationTokenSource.Token;
    var connection = new NpgsqlConnection(Settings.ConnectionString);
    await using var connection1 = connection.ConfigureAwait(false);
    await connection.OpenAsync(ct).ConfigureAwait(false);
    //use a command for each message
    {
        var tuple = Enumerable.Range(0, result).Select(i =>
            dictionary.ElementAt(i % generatorLength));

        foreach (var s in dictionary.Keys.Select((key, i) => $"Publishing {(messageCount + (result % generatorLength > i ? 1 : 0))} {key}").ToList())
            await Console.Out.WriteLineAsync(s);

        foreach (var message in tuple.Select(pair => pair.Value()))
        {
            var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
            try
            {
                await MessageAppender.AppendAsync(message, publisherOptions, connection, transaction, ct).ConfigureAwait(false);
                //OR with typed version
                //switch (message)
                //{
                //    case UserCreated m:
                //        await MessageAppender.AppendAsync(m, resolver, connection, transaction, ct).ConfigureAwait(false);
                //        break;
                //    case UserDeleted m:
                //        await MessageAppender.AppendAsync( m, resolver, connection, transaction, ct).ConfigureAwait(false);
                //        break;
                //    case UserModified m:
                //        await MessageAppender.AppendAsync(m, resolver, connection, transaction, ct).ConfigureAwait(false);
                //        break;
                //    case UserSubscribed m:
                //        await MessageAppender.AppendAsync(m, resolver, connection, transaction, ct).ConfigureAwait(false);
                //        break;
                //}

                await transaction.CommitAsync(ct).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                await transaction.RollbackAsync(ct).ConfigureAwait(false);
                Console.WriteLine(e);
                throw;
            }
        }
        await Console.Out.WriteLineAsync($"Published {result} messages!");
    }
    //use a batch command
    //{
    //    var transaction = await connection.BeginTransactionAsync(ct);
    //    try
    //    {
    //        var @events = Enumerable.Range(0, result)
    //            .Select(i1 => new UserCreated(Guid.NewGuid(), Guid.NewGuid().ToString()));
    //        await MessageAppender.AppendAsync(@events, resolver, connection, transaction, ct);
    //    }
    //    catch (Exception e)
    //    {
    //        Console.WriteLine(e);
    //        throw;
    //    }
    //}
}

internal class Options(IEnumerable<string> messageTypes, int count)
{
    internal static readonly Dictionary<string, Func<object>> Generator = new()
    {
        { nameof(UserCreated), () => new UserCreated(Guid.NewGuid()) },
        { nameof(UserDeleted), () => new UserDeleted(Guid.NewGuid()) },
        { nameof(UserModified), () => new UserModified(Guid.NewGuid()) },
        { nameof(UserSubscribed), () => new UserSubscribed(Guid.NewGuid()) }
    };
    private static readonly int CountByType = TotalCount / Generator.Count;
    private static readonly int Mod = TotalCount % CountByType;
    private const int TotalCount = 10;

    [Option('t', "type", Required = true, HelpText = "Message type.", Separator = '|')]
    public IEnumerable<string> MessageTypes { get; } = messageTypes;

    [Option('c', "count", Required = true, HelpText = "Total number")]
    public int Count { get; } = count;

    [Usage]
    public static IEnumerable<Example> Examples =>
    [
        new Example($"Publish {string.Join(" and ", Generator.Keys.Select((type,i)=> $"{CountByType + (Mod>i?1:0)} {type}"))} messages", new Options(Generator.Keys, TotalCount))
    ];
}

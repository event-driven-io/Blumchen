using Blumchen.Database;
using Blumchen.Publisher;
using Blumchen.Serialization;
using Commons;
using Microsoft.Extensions.Logging;
using Npgsql;
using Publisher;
using UserCreated = Publisher.UserCreated;
using UserDeleted = Publisher.UserDeleted;
using UserModified = Publisher.UserModified;
using UserSubscribed = Publisher.UserSubscribed;

Console.Title = typeof(Program).Assembly.GetName().Name!;

var generator = new Dictionary<string, Func<object>>
{
    { nameof(UserCreated), () => new UserCreated(Guid.NewGuid()) },
    { nameof(UserDeleted), () => new UserDeleted(Guid.NewGuid()) },
    { nameof(UserModified), () => new UserModified(Guid.NewGuid()) },
    { nameof(UserSubscribed), () => new UserSubscribed(Guid.NewGuid()) }
};

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

do
{
    await Console.Out.WriteLineAsync("How many messages do you want to publish?(press CTRL+C to exit):");
    var line = Console.ReadLine();
    if (line != null && int.TryParse(line, out var result))
    {
        var generatorLength = generator.Count;
        var messageCount = result / generatorLength;
        var ct = cts.Token;
        var connection = new NpgsqlConnection(Settings.ConnectionString);
        await using var connection1 = connection.ConfigureAwait(false);
        await connection.OpenAsync(ct).ConfigureAwait(false);
        //use a command for each message
        {
            var tuple = Enumerable.Range(0, result).Select(i =>
                generator.ElementAt(i % generatorLength));

            foreach (var s in generator.Keys.Select((key, i) => $"Publishing {(messageCount + (result % generatorLength > i ? 1 : 0))} {key}").ToList())
                await Console.Out.WriteLineAsync(s);

            foreach (var message in tuple.Select(_ => _.Value()))
            {
                var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
                try
                {
                    await MessageAppender.AppendAsync(message, resolver, connection, transaction, ct).ConfigureAwait(false);
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
} while (true);

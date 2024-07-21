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
Console.WriteLine("How many messages do you want to publish?(press CTRL+C to exit):");
var cts = new CancellationTokenSource();

var generator = new Func<object>[]
{
    () => new UserCreated(Guid.NewGuid()),
    () => new UserDeleted(Guid.NewGuid()),
    () => new UserModified(Guid.NewGuid()),
    () => new UserSubscribed(Guid.NewGuid())
};

do
{

    var line = Console.ReadLine();
    if (line != null && int.TryParse(line, out var result))
    {
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

        var messages = result / 4;
        var ct = cts.Token;
        var connection = new NpgsqlConnection(Settings.ConnectionString);
        await using var connection1 = connection.ConfigureAwait(false);
        await connection.OpenAsync(ct).ConfigureAwait(false);
        //use a command for each message
        {
            var @events = Enumerable.Range(0, result).Select(i =>
                generator[i % generator.Length]());
            await Console.Out.WriteLineAsync($"Publishing {messages + ((result % 3 > 0) ? 1 : 0)} {nameof(UserCreated)}");
            await Console.Out.WriteLineAsync($"Publishing {messages + ((result % 3 > 1) ? 1 : 0)} {nameof(UserDeleted)}");
            await Console.Out.WriteLineAsync($"Publishing {messages + ((result % 3 > 2) ? 1 : 0)} {nameof(UserModified)}");
            await Console.Out.WriteLineAsync($"Publishing {messages} {nameof(UserSubscribed)}");
            foreach (var @event in @events)
            {
                var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
                try
                {
                    switch (@event)
                    {
                        case UserCreated m:
                            await MessageAppender.AppendAsync(m, resolver, connection, transaction, ct).ConfigureAwait(false);
                            break;
                        case UserDeleted m:
                            await MessageAppender.AppendAsync( m, resolver, connection, transaction, ct).ConfigureAwait(false);
                            break;
                        case UserModified m:
                            await MessageAppender.AppendAsync(m, resolver, connection, transaction, ct).ConfigureAwait(false);
                            break;
                        case UserSubscribed m:
                            await MessageAppender.AppendAsync(m, resolver, connection, transaction, ct).ConfigureAwait(false);
                            break;
                    }

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

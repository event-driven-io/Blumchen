using Blumchen.Publications;
using Blumchen.Serialization;
using Commons;
using Npgsql;
using Publisher;
using UserCreated = Publisher.UserCreated;
using UserDeleted = Publisher.UserDeleted;
using UserModified = Publisher.UserModified;

Console.Title = typeof(Program).Assembly.GetName().Name!;
Console.WriteLine("How many messages do you want to publish?(press CTRL+C to exit):");

var resolver = new PublisherSetupOptionsBuilder()
    .JsonContext(SourceGenerationContext.Default)
    .NamingPolicy(new AttributeNamingPolicy())
    .Build();

do
{

    var line = Console.ReadLine();
    if (line != null && int.TryParse(line, out var result))
    {
        var cts = new CancellationTokenSource();

        var ct = cts.Token;
        var connection = new NpgsqlConnection(Settings.ConnectionString);
        await using var connection1 = connection.ConfigureAwait(false);
        await connection.OpenAsync(ct).ConfigureAwait(false);
        //use a command for each message
        {
            var @events = Enumerable.Range(0, result).Select(i =>
                (i % 3) switch
                {
                    0 => new UserCreated(Guid.NewGuid()) as object,
                    1 => new UserDeleted(Guid.NewGuid()),
                    _ => new UserModified(Guid.NewGuid())
                });
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

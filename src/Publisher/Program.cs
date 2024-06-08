using Commons;
using Npgsql;
using PostgresOutbox.Table;
using Publisher;
using UserCreated = Publisher.UserCreated;
using UserDeleted = Publisher.UserDeleted;

Console.Title = typeof(Program).Assembly.GetName().Name!;
Console.WriteLine("How many messages do you want to publish?(press CTRL+C to exit):");

var resolver = new PublisherTypesResolver();

do
{

    var line = Console.ReadLine();
    if (line != null && int.TryParse(line, out var result))
    {
        var cts = new CancellationTokenSource();

        var ct = cts.Token;
        await using var connection = new NpgsqlConnection(Settings.ConnectionString);
        await connection.OpenAsync(ct);
        //use a command for each message
        {
            var @events = Enumerable.Range(0, result).Select(i =>
                int.IsEvenInteger(i)
                    ? new UserCreated(Guid.NewGuid(), Guid.NewGuid().ToString()) as object
                    : new UserDeleted(Guid.NewGuid(), Guid.NewGuid().ToString()));
            foreach (var @event in @events)
            {
                var transaction = await connection.BeginTransactionAsync(ct);
                try
                {
                    switch (@event)
                    {
                        case UserCreated c:
                            await MessageAppender.AppendAsync("outbox", c, resolver, connection, transaction, ct);
                            break;
                        case UserDeleted d:
                            await MessageAppender.AppendAsync("outbox", d, resolver, connection, transaction, ct);
                            break;
                    }

                    await transaction.CommitAsync(ct);
                }
                catch (Exception e)
                {
                    await transaction.RollbackAsync(ct);
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
        //        await EventsAppender.AppendAsync("outbox", @events, resolver, connection, transaction, ct);
        //    }
        //    catch (Exception e)
        //    {
        //        Console.WriteLine(e);
        //        throw;
        //    }
        //}
    }
} while (true);




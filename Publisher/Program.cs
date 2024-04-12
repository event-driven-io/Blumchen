using Commons;
using Commons.Events;
using Npgsql;
using PostgresOutbox.Events;
#pragma warning disable CS8509 // The switch expression does not handle all possible values of its input type (it is not exhaustive).

Console.Title = typeof(Program).Assembly.GetName().Name!;
Console.WriteLine("How many messages do you want to publish?(press CTRL+C to exit):");

var resolver = new TypeResolver();

do
{

    var line = Console.ReadLine();
    if (line != null && int.TryParse(line, out var result))
    {
        var cts = new CancellationTokenSource();

        var ct = cts.Token;
        await using var connection = new NpgsqlConnection(Settings.ConnectionString);
        await connection.OpenAsync(ct);
        var transaction = await connection.BeginTransactionAsync(ct);
        try
        {
            //use a command for each message
            {
                var @events = Enumerable.Range(0, result).Select(i =>
                    int.IsEvenInteger(i)
                        ? new UserCreated(Guid.NewGuid(), Guid.NewGuid().ToString()) as object
                        : new UserDeleted(Guid.NewGuid(), Guid.NewGuid().ToString()));
                foreach (var @event in @events)
                {
                    switch (@event)
                    {
                        case UserCreated c:
                            await EventsAppender.AppendAsync("outbox", c, resolver, connection, transaction, ct);
                            break;
                        case UserDeleted d:
                            await EventsAppender.AppendAsync("outbox", d, resolver, connection, transaction, ct);
                            break;
                    }
                }

                ;
            }
            //use a batch command
            {
                var @events = Enumerable.Range(0, result)
                    .Select(i1 => new UserCreated(Guid.NewGuid(), Guid.NewGuid().ToString()));
                await EventsAppender.AppendAsync("outbox", @events, resolver, connection, transaction, ct);
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
} while (true);




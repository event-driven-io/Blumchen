using Commons;
using Commons.Events;
using Npgsql;
using PostgresOutbox.Events;

#pragma warning disable CS8601 // Possible null reference assignment.
Console.Title = typeof(Program).Assembly.GetName().Name;
#pragma warning restore CS8601 // Possible null reference assignment.
var done = false;
Console.WriteLine("How many messages do you want to publish?(press CTRL+Z to exit):");
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
                var @events = Enumerable.Range(0, result).Select(i1 => new UserCreated(Guid.NewGuid(), Guid.NewGuid().ToString()));
                foreach (var @event in @events)
                    await EventsAppender.AppendAsync("events", @event, connection, transaction, ct);
                
            }
            //use a batch command
            {
                var @events = Enumerable.Range(0, result).Select(i1 => new UserCreated(Guid.NewGuid(), Guid.NewGuid().ToString()));
                await EventsAppender.AppendAsync("events", @events, connection, transaction, ct);
            }

            await transaction.CommitAsync(ct);

            done = !done;

        }
        catch (Exception e)
        {
            await transaction.RollbackAsync(ct);
            Console.WriteLine(e);
            throw;
        }
        

    }

} while (done);




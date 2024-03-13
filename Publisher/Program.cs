using Commons;
using Commons.Events;
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
        for(var i = 0; i < result;i++)
        {
            var @event = new UserCreated(Guid.NewGuid(), Guid.NewGuid().ToString());
            await EventsAppender.AppendAsync("events", @event, Settings.ConnectionString, ct);
        }
        done = !done;
    }

} while (done);




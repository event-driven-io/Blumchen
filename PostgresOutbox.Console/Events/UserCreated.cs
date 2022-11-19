namespace PostgresOutbox.Console.Events;

public record UserCreated(
    Guid Id,
    string Name
);

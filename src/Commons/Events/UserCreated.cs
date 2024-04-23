namespace Commons.Events;

public record UserCreated(
    Guid Id,
    string Name
);

public record UserDeleted(
    Guid Id,
    string Name
);

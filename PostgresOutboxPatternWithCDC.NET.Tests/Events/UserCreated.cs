namespace PostgresOutboxPatternWithCDC.NET.Tests.Events;

public record UserCreated(
    Guid Id,
    string Name
);

using PostgresOutbox.Serialization;

namespace Commons.Events;

[MessageUrn("user-created:v1")]
public record UserCreated(
    Guid Id,
    string Name
);

[MessageUrn("user-deleted:v1")]
public record UserDeleted(
    Guid Id,
    string Name
);

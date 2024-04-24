using PostgresOutbox.Serialization;

namespace Publisher;

[MessageUrn("user-created:v1")]
internal record UserCreated(
    Guid Id,
    string Name
);

[MessageUrn("user-deleted:v1")]
internal record UserDeleted(
    Guid Id,
    string Name
);

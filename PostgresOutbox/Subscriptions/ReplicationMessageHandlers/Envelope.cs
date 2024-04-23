namespace PostgresOutbox.Subscriptions.ReplicationMessageHandlers;

public interface IEnvelope;

internal sealed record OkEnvelope(object Value): IEnvelope;

internal sealed record KoEnvelope(Exception Error, long id): IEnvelope;

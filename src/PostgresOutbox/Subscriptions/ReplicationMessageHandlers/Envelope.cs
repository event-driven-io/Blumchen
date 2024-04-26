namespace PostgresOutbox.Subscriptions.ReplicationMessageHandlers;

public interface IEnvelope;

public sealed record OkEnvelope(object Value): IEnvelope;

public sealed record KoEnvelope(Exception Error, string Id): IEnvelope;

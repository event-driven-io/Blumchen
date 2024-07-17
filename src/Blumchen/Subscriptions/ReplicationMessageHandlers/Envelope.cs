namespace Blumchen.Subscriptions.ReplicationMessageHandlers;

public interface IEnvelope;

public sealed record OkEnvelope(object Value, string MessageType): IEnvelope;

public sealed record KoEnvelope(Exception Error, string Id): IEnvelope;

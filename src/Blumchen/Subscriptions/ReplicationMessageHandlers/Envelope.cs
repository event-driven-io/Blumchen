namespace Blumchen.Subscriptions.ReplicationMessageHandlers;
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

public interface IEnvelope;

public sealed record OkEnvelope(object Value): IEnvelope;

public sealed record KoEnvelope(Exception Error, string Id): IEnvelope;

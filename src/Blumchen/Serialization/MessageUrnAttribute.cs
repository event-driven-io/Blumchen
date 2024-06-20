using System.Collections.Concurrent;

namespace Blumchen.Serialization;
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
public class MessageUrnAttribute:
    Attribute
{
    /// <summary>
    /// </summary>
    /// <param name="urn">The urn value to use for this message type.</param>
    public MessageUrnAttribute(string urn)
    {
        ArgumentException.ThrowIfNullOrEmpty(urn, nameof(urn));

        if (urn.StartsWith(MessageUrn.Prefix))
            throw new ArgumentException($"Value should not contain the default prefix '{MessageUrn.Prefix}'.", nameof(urn));

        Urn = FormatUrn(urn);
    }

    public Uri Urn { get; }

    private static Uri FormatUrn(string urn)
    {
        var fullValue = MessageUrn.Prefix + urn;

        if (Uri.TryCreate(fullValue, UriKind.Absolute, out var uri))
            return uri;

        throw new UriFormatException($"Invalid URN: {fullValue}");
    }
}


public static class MessageUrn
{
    public const string Prefix = "urn:message:";

    private static readonly ConcurrentDictionary<Type, ICached> Cache = new();


    public static string ForTypeString(Type type) =>
        Cache.GetOrAdd(type,t =>
        {
            var attribute = Attribute.GetCustomAttribute(t, typeof(MessageUrnAttribute)) as MessageUrnAttribute ??
                            throw new NotSupportedException($"Attribute not defined fot type '{type}'");
            return new Cached(attribute.Urn, attribute.Urn.ToString());
        }).UrnString;


    private interface ICached
    {
        Uri Urn { get; }
        string UrnString { get; }
    }

    private record Cached(Uri Urn, string UrnString): ICached;
}

using System.Collections.Concurrent;

namespace Blumchen.Serialization;

public interface IRouted
{
    string Route { get; }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class MessageRoutedByUrnAttribute(string route):
    Attribute, IRouted
{
    public string Route { get; } = Format(route);

    private static string Format(string urn)
    {
        ArgumentException.ThrowIfNullOrEmpty(urn, nameof(urn));

        if (urn.StartsWith(MessageUrn.Prefix))
            throw new ArgumentException($"Value should not contain the default prefix '{MessageUrn.Prefix}'.", nameof(urn));

        return FormatUrn(urn).AbsoluteUri;
    }

    private static Uri FormatUrn(string urn)
    {
        var fullValue = MessageUrn.Prefix + urn;

        if (Uri.TryCreate(fullValue, UriKind.Absolute, out var uri))
            return uri;

        throw new UriFormatException($"Invalid URN: {fullValue}");
    }
}

internal static class MessageUrn
{
    public const string Prefix = "urn:message:";

    private static readonly ConcurrentDictionary<Type, ICached> Cache = new();
    
    public static string ForTypeString(Type type) =>
        Cache.GetOrAdd(type, t =>
        {
            var attribute = Attribute.GetCustomAttribute(t, typeof(MessageRoutedByUrnAttribute)) as MessageRoutedByUrnAttribute ??
                            throw new NotSupportedException($"Attribute not defined fot type '{type}'");
            return new Cached(attribute.Route);
        }).UrnString;


    private interface ICached
    {
        string UrnString { get; }
    }

    private record Cached(string UrnString): ICached;
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public class RawRoutedByUrnAttribute(string route): MessageRoutedByUrnAttribute(route);

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public class RawRoutedByStringAttribute(string name): Attribute, IRouted
{
    private static string Format(string name)
    {
        if(string.IsNullOrWhiteSpace(name))
            throw new FormatException($"Invalid {nameof(name)}: {name}.");

        return name;
    }
    public string Route { get; } = Format(name); 
}




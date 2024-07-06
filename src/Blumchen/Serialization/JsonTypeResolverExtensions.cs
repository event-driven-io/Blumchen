namespace Blumchen.Serialization;


public static class JsonTypeResolverExtensions
{
    internal static IEnumerable<string> Keys(this JsonTypeResolver? resolver) => resolver?.RegisteredTypes.Keys ?? Enumerable.Empty<string>();
    internal static IEnumerable<Type> Values(this JsonTypeResolver? resolver) => resolver?.RegisteredTypes.Values ?? Enumerable.Empty<Type>();
}

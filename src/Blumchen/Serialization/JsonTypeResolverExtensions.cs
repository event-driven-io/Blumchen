namespace Blumchen.Serialization;


public static class JsonTypeResolverExtensions
{
    internal static IEnumerable<string> Keys<T>(this ITypeResolver<T>? resolver) => resolver?.RegisteredTypes.Keys ?? Enumerable.Empty<string>();
    internal static IEnumerable<Type> Values<T>(this ITypeResolver<T>? resolver) => resolver?.RegisteredTypes.Values ?? Enumerable.Empty<Type>();
}

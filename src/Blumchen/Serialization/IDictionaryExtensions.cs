#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
namespace Blumchen.Serialization;


public static class DictionaryExtensions
{
    internal static IEnumerable<string> Keys(this JsonTypeResolver? resolver) => resolver?.RegisteredTypes.Keys ?? Enumerable.Empty<string>();
    internal static IEnumerable<Type> Values(this JsonTypeResolver? resolver) => resolver?.RegisteredTypes.Values ?? Enumerable.Empty<Type>();
}

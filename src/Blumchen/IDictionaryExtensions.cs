namespace Blumchen;

internal static class IDictionaryExtensions
{
    public static TR FindByMultiKey<T, TR>(this IDictionary<T, TR> registry, params T[] parameters)
        where T : class =>
        !registry.TryGetValue(parameters[0], out var value) ? registry.FindByMultiKey(parameters[1..parameters.Length]) : value;
}

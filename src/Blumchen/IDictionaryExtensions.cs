namespace Blumchen;

internal static class IDictionaryExtensions
{
    public static TR? FindByMultiKey<T, TR>(this IDictionary<T, TR> registry, params T[] parameters) where T : class
    {
            if (parameters.Length == 0) return default;
            return registry.TryGetValue(parameters[0], out var value)
                ? value
                : FindByMultiKey<T,TR>(registry, parameters[1..parameters.Length]);
    }
}

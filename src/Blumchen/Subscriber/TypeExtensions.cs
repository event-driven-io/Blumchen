using Blumchen.Serialization;

namespace Blumchen.Subscriber;

internal static class TypeExtensions
{
    public static IEnumerable<IRouted> GetAttributes<T>(this Type type)
        where T : Attribute, IRouted =>
        type.GetCustomAttributes(typeof(T), false).OfType<IRouted>();
}

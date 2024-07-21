using System.Collections;
using Blumchen.Subscriber;

namespace Blumchen;

internal static class Ensure
{
    public static void Null<T>(T value, params object[] parameters) => new NullTrait<T>().IsValid(value, parameters);
    public static void NotNull<T>(T value, params object[] parameters) => new NotNullTrait<T>().IsValid(value, parameters);
    public static void NotEmpty<T>(T value, params object[] parameters) => new NotEmptyTrait<T>().IsValid(value, parameters);
    public static void Empty<T>(T value, params object[] parameters) => new EmptyTrait<T>().IsValid(value, parameters);
}

internal abstract class Validable<T>(Func<T, bool> condition, string errorFormat)
{
    public void IsValid(T value, params object[] parameters)
    {
        if (!condition(value))
            throw new ConfigurationException(string.Format(errorFormat, parameters));
    }
}

internal class NullTrait<T>(): Validable<T>(v => v is null, $"`{{0}}` method on {nameof(OptionsBuilder)} called more then once");
internal class NotNullTrait<T>(): Validable<T>(v => v is not null, $"`{{0}}` method not called on {nameof(OptionsBuilder)}");
internal class NotEmptyTrait<T>(): Validable<T>(v => v is ICollection { Count: > 0 }, $"No `{{0}}` method called on {nameof(OptionsBuilder)}");
internal class EmptyTrait<T>(): Validable<T>(v => v is ICollection { Count: 0 }, $"`{{0}}` cannot be mixed with other consuming strategies");

using System.Collections;
using Blumchen.Serialization;
using Blumchen.Subscriber;

namespace Blumchen;

internal static class Ensure
{
    public static void RawUrn<T,TR>(T value, string parameters) => new RawUrnTrait<T,TR>().IsValid(value, parameters);
    public static void Null<T>(T value, string parameters) => new NullTrait<T>().IsValid(value, parameters);
    public static void NotNull<T>(T value, string parameters) => new NotNullTrait<T>().IsValid(value, parameters);
    public static void NotEmpty<T>(T value, string parameters) => new NotEmptyTrait<T>().IsValid(value, parameters);
    public static void Empty<T>(T value, string parameters) => new EmptyTrait<T>().IsValid(value, parameters);
    public static bool Empty<T, TU>(T value1, TU value2, params string[] parameters) =>
        new EmptyTrait<T>().IsValid(value1, parameters) && new EmptyTrait<TU>().IsValid(value2, parameters);
}

internal abstract class Validable<T>(Func<T, bool> condition, string errorFormat)
{
    public bool IsValid(T value, params string[] parameters)
    {
        if (!condition(value))
            throw new ConfigurationException(string.Format(errorFormat, parameters));
        return true;
    }
}

internal class RawUrnTrait<T,TR>(): Validable<T>(v => v is ICollection { Count: > 0 }, $"`{nameof(RawRoutedByUrnAttribute)}` missing on `{typeof(TR).Name}` message type");
internal class NullTrait<T>(): Validable<T>(v => v is null, $"`{{0}}` method on {nameof(OptionsBuilder)} called more then once");
internal class NotNullTrait<T>(): Validable<T>(v => v is not null, $"`{{0}}` method not called on {nameof(OptionsBuilder)}");
internal class NotEmptyTrait<T>(): Validable<T>(v => v is ICollection { Count: > 0 }, $"No `{{0}}` method called on {nameof(OptionsBuilder)}");
internal class EmptyTrait<T>(): Validable<T>(v => v is ICollection { Count: 0 }, $"`{{0}}` cannot be mixed with other consuming strategies");

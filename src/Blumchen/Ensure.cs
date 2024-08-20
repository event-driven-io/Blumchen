using System.Collections;
using Blumchen.Serialization;
using Blumchen.Subscriber;
using ImTools;

namespace Blumchen;

internal static class Ensure
{
    internal const string CannotBeMixedWithOtherConsumingStrategies = "`{0}` cannot be mixed with other consuming strategies";

    internal const string RawRoutedByUrnErrorFormat = $"`{nameof(RawRoutedByUrnAttribute)}` missing on `{{0}}` message type";

    public static void RawUrn<T>(T value, params object?[] parameters) => new RawUrnTrait<T>().IsValid(value, parameters);
    public static void Null<T>(T value, string parameters) => new NullTrait<T>().IsValid(value, parameters);
    public static void NotNull<T>(T value, string parameters) => new NotNullTrait<T>().IsValid(value, parameters);
    public static void NotEmpty<T>(T value, string parameters) => new NotEmptyTrait<T>().IsValid(value, parameters);
    public static void Empty<T>(T value, string parameters) => new EmptyTrait<T>().IsValid(value, parameters);
    public static void And<T,TU>(Validable<T> left, T v1, Validable<TU> right, TU v2, params object?[] parameters) =>
        _ = left.IsValid(v1, parameters) && right.IsValid(v2, parameters);
}

internal abstract record Validable<T>(Func<T, bool> Condition, string ErrorFormat)
{
    public bool IsValid(T value, params object?[] parameters)
    {
        if (!Condition(value))
            throw new ConfigurationException(string.Format(ErrorFormat, parameters));
        return true;
    }
}

internal record RawUrnTrait<T>(): Validable<T>(v => v is ICollection { Count: > 0 },
    Ensure.RawRoutedByUrnErrorFormat);

internal record NullTrait<T>()
    : Validable<T>(v => v is null, $"`{{0}}` method on {nameof(OptionsBuilder)} called more then once");

internal record NotNullTrait<T>()
    : Validable<T>(v => v is not null, $"`{{0}}` method not called on {nameof(OptionsBuilder)}");

internal record NotEmptyTrait<T>(): Validable<T>(v => v is ICollection { Count: > 0 },
    $"No `{{0}}` method called on {nameof(OptionsBuilder)}");

internal record EmptyTrait<T>()
    : Validable<T>(v => v is ICollection { Count: 0 }, Ensure.CannotBeMixedWithOtherConsumingStrategies);

internal record BoolTrait<T>(Func<T, bool> Condition, string ErrorFormat)
    : Validable<T>(Condition, ErrorFormat);

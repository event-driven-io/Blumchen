namespace Blumchen.Subscriptions;

internal class Memoizer<T, TU, TR>(Func<T, TU, TR> func)
    where T : notnull
{
    private readonly Dictionary<T, TR> _memoizer = new();

    private TR Caller(T value1, TU value2)
    {
        if (_memoizer.TryGetValue(value1, out var result))
            return result;
        return _memoizer[value1] = func(value1, value2);
    }

    public static Func<T, TU, TR> Execute(Func<T, TU, TR> func)
    {
        var memoizer = new Memoizer<T, TU, TR>(func);
        return (x, y) => memoizer.Caller(x, y);
    }
}
internal class Memoizer<T, TU, TR,TT>(Func<T, TU, TR,TT> func)
    where T : notnull
{
    private readonly Dictionary<T, TT> _memoizer = new();

    private TT Caller(T value1, TU value2, TR value3)
    {
        if (_memoizer.TryGetValue(value1, out var result))
            return result;
        return _memoizer[value1] = func(value1, value2, value3);
    }

    public static Func<T, TU, TR, TT> Execute(Func<T, TU, TR,TT> func)
    {
        var memoizer = new Memoizer<T, TU, TR,TT>(func);
        return (x, y,z) => memoizer.Caller(x, y,z);
    }
}

namespace PostgresOutbox.Serialization;

public interface INamingPolicy
{
    Func<Type, string> Bind { get; }
}

public abstract record NamingPolicy(Func<Type, string> Bind):INamingPolicy
{
    public Func<Type, string> Bind { get; } = Bind;
}

internal record FQNNamingPolicy(): NamingPolicy(type => type.FullName!);


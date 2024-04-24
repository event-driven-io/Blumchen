namespace PostgresOutbox.Serialization;

public interface INamingPolicy
{
    Func<Type, string> Bind { get; }
}

public abstract record NamingPolicy(Func<Type, string> Bind):INamingPolicy
{
    public Func<Type, string> Bind { get; } = Bind;
}

//This should be used in shared kernel scenario where common library is shared between Pub and Sub
public record FQNNamingPolicy(): NamingPolicy(type => type.FullName!);
//This policy is better suited for distributed components
public record AttributeNamingPolicy(): NamingPolicy(MessageUrn.ForTypeString);

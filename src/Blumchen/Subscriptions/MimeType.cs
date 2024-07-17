namespace Blumchen.Subscriptions;

public abstract record MimeType(string mimeType)
{
    public record Json(): MimeType("application/json");
}

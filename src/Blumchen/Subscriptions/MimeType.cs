namespace Blumchen.Subscriptions;

#pragma warning disable CS1591
public abstract record MimeType(string mimeType)
{
    public record Json(): MimeType("application/json");
}

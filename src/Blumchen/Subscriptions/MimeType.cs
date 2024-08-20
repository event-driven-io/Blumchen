namespace Blumchen.Subscriptions;

public abstract record MimeType(string mimeType)
{
    internal record JsonMimeType(): MimeType("application/json");

    public static MimeType Json => new JsonMimeType();

}

namespace Blumchen.Streams
{
    internal static class StreamExtensions
    {
        public static SohSkippingStream ToSohSkippingStream(this Stream stream) => new(inner: stream);
    }
}

namespace Blumchen.Serialization;

internal class SOHSkippingStream(Stream inner): Stream
{
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => inner.Length;

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int ReadByte()
    {
        var initialPosition = inner.Position;
        var result = inner.ReadByte();
        if (result == 1 && initialPosition == 0)
        {
            result = inner.ReadByte();
        }

        return result;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var totalRead = 0;
        if (inner.Position == 0)
        {
            var readBytes = inner.Read(buffer, 0, 1);
            if (readBytes <= 0)
            {
                return readBytes;
            }

            if (buffer[0] != 1)
            {
                offset += 1;
                count -= 1;
                totalRead = 1;
            }
        }

        return totalRead + inner.Read(buffer, offset, count);
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count,
        CancellationToken cancellationToken)
    {
        var totalRead = 0;
        if (inner.Position == 0)
        {
            var readBytes = await inner.ReadAsync(buffer.AsMemory(0, 1), cancellationToken).ConfigureAwait(false);
            if (readBytes <= 0)
            {
                return readBytes;
            }

            if (buffer[0] != 1)
            {
                offset += 1;
                count -= 1;
                totalRead = 1;
            }
        }

        return totalRead + await inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        var totalRead = 0;
        if (inner.Position == 0)
        {
            var singleByteBuffer = buffer[..1];

            var readBytes = await inner.ReadAsync(singleByteBuffer, cancellationToken).ConfigureAwait(false);
            if (readBytes <= 0)
            {
                return readBytes;
            }

            if (singleByteBuffer.Span[0] != 1)
            {
                totalRead = 1;
                buffer = buffer[1..];
            }
        }

        return totalRead + await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
    }

    public override int Read(Span<byte> buffer)
    {
        var totalRead = 0;
        if (inner.Position == 0)
        {
            var singleByteBuffer = buffer[..1];
            var readBytes = inner.Read(singleByteBuffer);
            if (readBytes <= 0)
            {
                return readBytes;
            }

            if (singleByteBuffer[0] != 1)
            {
                totalRead = 1;
                buffer = buffer[1..];
            }
        }

        return totalRead + inner.Read(buffer);
    }

    public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback,
        object? state)
    {
        throw new NotSupportedException();
    }

    public override int EndRead(IAsyncResult asyncResult)
    {
        throw new NotSupportedException();
    }


    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    public override void Flush()
    {
        throw new NotSupportedException();
    }

    protected override void Dispose(bool disposing)
    {
        inner.Dispose();
    }

    public override ValueTask DisposeAsync()
    {
        return inner.DisposeAsync();
    }
}

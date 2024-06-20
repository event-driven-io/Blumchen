namespace Blumchen.Streams
{
    internal class SohSkippingStream(Stream inner): Stream
    {
        public override int ReadByte()
        {
            var result = inner.ReadByte();
            if (result == 1 && inner.Position == 0) result = inner.ReadByte();
            return result;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (inner.Position != 0) return inner.Read(buffer, offset, count);
            var readBytes = inner.Read(buffer, 0, 1);
            if (readBytes <= 0) return readBytes;

            if (buffer[0] == 1) return inner.Read(buffer, offset, count);
            offset += 1;
            count -= 1;
            return 1 + inner.Read(buffer, offset, count);
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count,
            CancellationToken cancellationToken)
        {
            if (inner.Position != 0)
                return await inner.ReadAsync(new Memory<byte>(buffer, offset, count), cancellationToken)
                    .ConfigureAwait(false);
            var readBytes = await inner.ReadAsync(new Memory<byte>(buffer, 0, 1), cancellationToken)
                .ConfigureAwait(false);
            if (readBytes <= 0) return readBytes;

            if (buffer[0] == 1)
                return await inner.ReadAsync(new Memory<byte>(buffer, offset, count), cancellationToken)
                    .ConfigureAwait(false);
            offset += 1;
            count -= 1;
            return 1 + await inner.ReadAsync(new Memory<byte>(buffer, offset, count), cancellationToken)
                .ConfigureAwait(false);
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (inner.Position != 0)
                return await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            var singleByteBuffer = buffer[..1];

            var readBytes = await inner.ReadAsync(singleByteBuffer, cancellationToken).ConfigureAwait(false);
            if (readBytes <= 0) return readBytes;

            if (singleByteBuffer.Span[0] == 1)
                return await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            buffer = buffer[1..];

            return 1 + await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        public override int Read(Span<byte> buffer)
        {
            if (inner.Position != 0) return inner.Read(buffer);
            var readBytes = inner.Read(buffer[..1]);
            if (readBytes <= 0) return readBytes;

            if (buffer[..1][0] == 1) return inner.Read(buffer);
            buffer = buffer[1..];

            return 1 + inner.Read(buffer);
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

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => inner.Length;

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
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
}

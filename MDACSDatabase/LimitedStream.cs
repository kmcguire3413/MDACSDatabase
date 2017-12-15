using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MDACS.Database
{
    class LimitedStream : Stream
    {
        private Stream inner;
        private ulong read_left;

        public LimitedStream(Stream inner, ulong read_left)
        {
            this.inner = inner;
            this.read_left = read_left;
        }

        public override bool CanRead => inner.CanRead;

        public override bool CanSeek => inner.CanSeek;

        public override bool CanWrite => inner.CanWrite;

        public override long Length => (long)read_left;

        // TODO: This needs to be adjusted to work properly.
        public override long Position { get; set; }

        public override void Flush()
        {
            inner.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (read_left == 0)
            {
                // Pretend to be at the end of the stream.
                return -1;
            }

            if ((ulong)count <= read_left)
            {
                read_left -= (ulong)count;
            } else
            {
                // Only allow the reading of so much of the stream.
                count = (int)read_left;
                read_left = 0;
            }

            return inner.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return inner.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            inner.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            inner.Write(buffer, offset, count);
        }
    }
}

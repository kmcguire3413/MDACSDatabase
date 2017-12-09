using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MDACS.Server
{
    struct BiAccessChunk
    {
        public byte[] chunk;
        public int amount_read;
        public int actual_size;
    }

    class BiAccessChunkedStream : Stream
    {
        private List<BiAccessChunk> chunks;
        private int amount_pushed;
        private int max_push;
        private AsyncManualResetEvent pushev;
        private AsyncManualResetEvent pullev;

        public BiAccessChunkedStream(int max_push)
        {
            this.amount_pushed = 0;
            this.chunks = new List<BiAccessChunk>();
            this.max_push = max_push;
            this.pushev = new AsyncManualResetEvent();
            this.pullev = new AsyncManualResetEvent();
        }

        public async Task<bool> AddChunk(byte[] chunk, int actual_size)
        {
            BiAccessChunk _chunk;

            _chunk.chunk = chunk;
            _chunk.amount_read = 0;
            _chunk.actual_size = actual_size;

            Monitor.Enter(chunks);

            pushev.Reset();

            try
            {
                chunks.Add(_chunk);
                this.amount_pushed += actual_size;

                this.pullev.Set();

                if (this.amount_pushed > this.max_push)
                {
                    Monitor.Exit(chunks);
                    await this.pushev.WaitAsync();
                    Monitor.Enter(chunks);
                }
            }
            finally
            {
                Monitor.Exit(chunks);
            }

            return true;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => 0;

        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int amt_read;

            // Wait until we are signaled that there is data.
            this.pullev.WaitAsync().Wait();

            lock (chunks)
            {
                this.pullev.Reset();

                if (chunks.Count < 0)
                {
                    return 0;
                }

                var tmp = chunks[0];

                if (tmp.actual_size < 1)
                {
                    return 0;
                }

                if (count < tmp.actual_size - tmp.amount_read)
                {
                    // Only read what caller specified.
                    Array.Copy(tmp.chunk, tmp.amount_read, buffer, offset, count);

                    tmp.amount_read += count;

                    amt_read = count;
                }
                else
                {
                    // Read as much as possible but no more than caller specified.
                    var amt = tmp.actual_size - tmp.amount_read;

                    Array.Copy(tmp.chunk, tmp.amount_read, buffer, offset, amt);

                    // Remove this chunk as it is spent/used/done.
                    chunks.RemoveAt(0);

                    amt_read = amt;
                }
            }

            amount_pushed -= amt_read;

            if (amount_pushed < max_push)
            {
                pushev.Set();
            }

            return amt_read;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }
}

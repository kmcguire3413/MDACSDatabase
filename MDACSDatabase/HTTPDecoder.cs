using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace MDACS.Server
{
    class HTTPDecoderBodyType
    {
        public enum MyType
        {
            NoBody,
            ChunkedEncoding,
            ContentLength,
        }

        public MyType type;
        public long size;

        private HTTPDecoderBodyType(MyType type, long size)
        {
            this.type = type;
            this.size = size;
        }

        public static HTTPDecoderBodyType NoBody()
        {
            return new HTTPDecoderBodyType(MyType.NoBody, 0);
        }

        public static HTTPDecoderBodyType ChunkedEncoding()
        {
            return new HTTPDecoderBodyType(MyType.ChunkedEncoding, 0);
        }

        public static HTTPDecoderBodyType ContentLength(long size)
        {
            return new HTTPDecoderBodyType(MyType.ContentLength, size);
        }
    }

    class HTTPDecoder
    {
        private Stream s;
        private byte[] ms;
        private int last_write_pos;

        public HTTPDecoder(Stream s)
        {
            this.s = s;
            this.ms = new byte[1024 * 16];
            this.last_write_pos = 0;
        }

        public async Task<int> ReadChunk()
        {
            if (ms.Length - last_write_pos < 1)
            {
                throw new Exception("Out of buffer space looking for header.");
            }

            var cnt = await s.ReadAsync(ms, last_write_pos, ms.Length - last_write_pos);
            last_write_pos += cnt;

            return cnt;
        }

        public async Task<byte[]> TakeChunkPrefix(int size)
        {
            var tmp = new byte[size];

            // Copy into output buffer `tmp`.
            Array.Copy(ms, tmp, size);
            // Copy postfix onto prefix at beginning.
            Array.Copy(ms, size, ms, 0, last_write_pos - size);

            if (last_write_pos - size > 0)
            {
                last_write_pos = last_write_pos - size;
            } else
            {
                last_write_pos = 0;
            }

            return tmp;
        }

        public async Task<List<String>> ReadHeader()
        {
            var header = new List<String>();
            int line_end_pos = -1;

            do
            {
                if (line_end_pos < 0)
                {
                    var cnt = await ReadChunk();

                    Console.WriteLine("cnt={0} from ReadChunk", cnt);

                    if (cnt < 1)
                    {
                        return null;
                    }
                }

                Console.WriteLine(String.Format("ReadHeader: last_write_pos={0}", last_write_pos));

                // Check if we have a complete line in the buffer.
                line_end_pos = Array.FindIndex(ms, 0, last_write_pos, b => b == 10);

                Console.WriteLine(String.Format("PRE:line_end_pos={0}", line_end_pos));

                if (line_end_pos > -1)
                {
                    var line = await TakeChunkPrefix(line_end_pos + 1);
                    var line_utf8_str = Encoding.UTF8.GetString(line).Trim();

                    if (line_utf8_str.Length == 0)
                    {
                        Console.WriteLine("Read blank line after header.");
                        break;
                    }
                    else
                    {
                        Console.WriteLine("line_utf8_str={0}", line_utf8_str);
                        header.Add(line_utf8_str);
                    }
                }

                Console.WriteLine(String.Format("POST:line_end_pos={0}", line_end_pos));
            } while (true);

            Console.WriteLine("Done reading header.");

            return header;
        }

        private async Task<(byte[], int, String)> ReadLineOutOfStream(int max_line_size, byte[] slack, int slack_size)
        {
            var buf = new byte[max_line_size];
            int pos = 0;
            int ndx = 0;

            if (slack != null && slack_size > 0)
            {
                Array.Copy(slack, buf, slack_size);
                pos = slack_size;
            }

            do
            {
                var cnt = await s.ReadAsync(buf, pos, buf.Length - pos);

                if (cnt < 1)
                {
                    throw new Exception("Error when reading line out of stream.");
                }

                pos += cnt;

                ndx = Array.IndexOf(buf, 10);
            } while (ndx < 0);

            var line = Encoding.UTF8.GetString(buf, 0, ndx);
            Array.Copy(buf, ndx + 1, buf, 0, pos - (ndx + 1));

            return (buf, pos - (ndx + 1), line);
        }

        public async Task<Stream> ReadBody(HTTPDecoderBodyType body_type)
        {
            var os = new BiAccessChunkedStream(1024 * 16);

            switch (body_type.type)
            {
                case HTTPDecoderBodyType.MyType.NoBody:
                    return os as Stream;
                case HTTPDecoderBodyType.MyType.ChunkedEncoding:
#pragma warning disable 4014
                    Task.Run(async () =>
                    {
                        byte[] _slack = null;
                        int _slack_size = 0;

                        do
                        {
                            var (slack, slack_size, line) = await ReadLineOutOfStream(256, _slack, _slack_size);

                            _slack = slack;
                            _slack_size = slack_size;

                            if (line.Length == 0)
                            {
                                // A blank line happens after a chunk. It simplified the logic below by re-using
                                // the logic above.
                                continue;
                            }

                            long chunk_size = Convert.ToUInt32(line, 16);

                            if (chunk_size == 0)
                            {
                                break;
                            }

                            // Read chunk.
                            if (chunk_size <= _slack_size)
                            {
                                // Special case where chunk is actually less than the slack size.
                                var tmpbuf = new byte[chunk_size];

                                Array.Copy(_slack, 0, tmpbuf, 0, chunk_size);

                                var r2 = await os.AddChunk(tmpbuf, (int)chunk_size);

                                if (chunk_size < _slack_size)
                                {
                                    // Less than. At least one byte left in slack.
                                    Array.Copy(_slack, chunk_size, _slack, 0, _slack_size - chunk_size);
                                    _slack_size -= (int)chunk_size;
                                }
                                else
                                {
                                    // Took everything. Exact size.
                                    _slack = null;
                                    _slack_size = 0;
                                }
                            }
                            else
                            {
                                // Normal case where chunk is larger than the slack size.
                                var r2 = await os.AddChunk(_slack, _slack_size);

                                var tmp = new byte[1024 * 4];

                                // Now, read the remaining.
                                do
                                {
                                    var cnt = await s.ReadAsync(tmp, 0, (int)Math.Min(chunk_size, tmp.Length));

                                    if (cnt < 1)
                                    {
                                        break;
                                    }

                                    chunk_size -= cnt;

                                    var r3 = await os.AddChunk(tmp, cnt);
                                } while (chunk_size > 0);
                            }
                        } while (true);

                        var r4 = await os.AddChunk(null, 0);
                    });
#pragma warning restore 4014
                    return os;
                case HTTPDecoderBodyType.MyType.ContentLength:
#pragma warning disable 4014
                    Task.Run(async () =>
                    {
                        var read_need = body_type.size;

                        do
                        {
                            var buf = new byte[Math.Max(1024 * 4, read_need)];
                            var cnt = await s.ReadAsync(buf, 0, buf.Length);

                            if (cnt < 1)
                            {
                                break;
                            }

                            read_need -= cnt;

                            var result = await os.AddChunk(buf, cnt);
                            // Get unstuck by having result be false from a timeout inside AddChunk then
                            // just free wheel the data into the void and continue onward.
                        } while (read_need > 0);
                    });
#pragma warning restore 4014
                    return os;
                default:
                    return os;
            }
        }
    }
}

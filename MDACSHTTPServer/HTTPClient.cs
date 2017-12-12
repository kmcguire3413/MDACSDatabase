#define PROXY_HTTP_ENCODER_DEBUG

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace MDACS.Server
{
    public class ProxyHTTPEncoder
    {
        public HTTPEncoder encoder;
        public AsyncManualResetEvent ready;
        public AsyncManualResetEvent done;
        public bool close_connection;

        public ProxyHTTPEncoder(HTTPEncoder encoder, bool close_connection)
        {
            this.encoder = encoder;
            this.ready = new AsyncManualResetEvent();
            this.done = new AsyncManualResetEvent();
            this.close_connection = close_connection;
        }

        public async Task Death()
        {

        }

        public async Task WriteQuickHeader(int code, String text)
        {
            var header = new Dictionary<String, String>();

            header.Add("$response_code", code.ToString());
            header.Add("$response_text", text);

            await WriteHeader(header);
        }

        /// <summary>
        /// Write the HTTP headers to the remote endpoint. The actual writing of the headers may or may
        /// not be delayed - depending on the implementation. The headers are likely to be sent once some
        /// response data has been written.
        /// </summary>
        /// <param name="header">The header.</param>
        /// <returns></returns>
        public async Task WriteHeader(Dictionary<String, String> header)
        {
            if (close_connection)
            {
                if (header.ContainsKey("connection"))
                {
                    header["connection"] = "close";
                }
                else
                {
                    header.Add("connection", "close");
                }
            }
            else
            {
                if (header.ContainsKey("connection"))
                {
                    header["connection"] = "keep-alive";
                }
                else
                {
                    header.Add("connection", "keep-alive");
                }
            }

            Console.WriteLine("!!! waiting on ready");
            await this.ready.WaitAsync();
            Console.WriteLine("!!! ready was good");
            await encoder.WriteHeader(header);
        }

        public async Task BodyWriteSingleChunk(String chunk)
        {
            byte[] chunk_bytes = Encoding.UTF8.GetBytes(chunk);
            await BodyWriteSingleChunk(chunk_bytes, 0, chunk_bytes.Length);
        }

        /// <summary>
        /// This will send a single chunk and use the content-length field of the HTTP response.
        /// </summary>
        /// <param name="chunk">The data to send.</param>
        /// <param name="offset">The offset within the data array.</param>
        /// <param name="length">The length of the chunk within the data array starting at the offset specified.</param>
        /// <returns></returns>
        public async Task BodyWriteSingleChunk(byte[] chunk, int offset, int length)
        {
            await this.ready.WaitAsync();
            await this.encoder.BodyWriteSingleChunk(chunk, offset, length);
            this.done.Set();
        }

        private async Task BodyWriteStreamInternal(Stream inpstream)
        {
            byte[] buf = new byte[1024 * 512];
            bool first_chunk = true;

#if PROXY_HTTP_ENCODER_DEBUG
            Console.WriteLine("{0}.BodyWriteStreamInternal: Now running.", this);
#endif

            do
            {
#if PROXY_HTTP_ENCODER_DEBUG
                Console.WriteLine("{0}.BodyWriteStreamInternal: Doing ReadAsync on stream.", this);
#endif
                var cnt = await inpstream.ReadAsync(buf, 0, buf.Length);

#if PROXY_HTTP_ENCODER_DEBUG
                Console.WriteLine("{0}.BodyWriteStreamInternal: Read cnt={1} buf={2} buf.Length={3}", this, cnt, buf, buf.Length);
#endif

                if (cnt < 1)
                {
#if PROXY_HTTP_ENCODER_DEBUG
                    Console.WriteLine("{0}.BodyWriteStreamInternal: End of stream.", this);
#endif
                    break;
                }

                if (first_chunk)
                {
#if PROXY_HTTP_ENCODER_DEBUG
                    Console.WriteLine("{0}.BodyWriteStreamInternal: First chunk.", this);
#endif
                    await this.encoder.BodyWriteFirstChunk(buf, 0, cnt);
                    first_chunk = false;
                }
                else
                {
#if PROXY_HTTP_ENCODER_DEBUG
                    Console.WriteLine("{0}.BodyWriteStreamInternal: Next chunk.", this);
#endif

                    await this.encoder.BodyWriteNextChunk(buf, 0, cnt);
                }
            } while (true);

            await this.encoder.BodyWriteNoChunk();

            this.done.Set();
        }

        /// <summary>
        /// This will spawn an asynchronous task which will continually read from the stream until
        /// it reaches the end. Each chunk of data read from the stream will be send as a chunk of
        /// a transfer-encoding chunked response.
        /// </summary>
        /// <param name="inpstream">The stream to read chunks from.</param>
        /// <returns></returns>
        public async Task<Task> BodyWriteStream(Stream inpstream)
        {
#if PROXY_HTTP_ENCODER_DEBUG
            Console.WriteLine("{0}.BodyWriteStream: Starting task to copy from stream into the real encoder.", this);
#endif
            await this.ready.WaitAsync();

            // Control needs to return to the caller. Do not `await` the result of this task.
            var runner = Task.Run(async () => {
                await BodyWriteStreamInternal(inpstream);
            });

            return runner;
        }
    }

    public class HTTPClient
    {
        private HTTPDecoder decoder;
        private HTTPEncoder encoder;
        private IHTTPServerHandler shandler;

        public HTTPClient(IHTTPServerHandler shandler, HTTPDecoder decoder, HTTPEncoder encoder)
        {
            this.decoder = decoder;
            this.encoder = encoder;
            this.shandler = shandler;
        }

        private HTTPClient()
        {

        }

        private Dictionary<String, String> LineHeaderToDictionary(List<String> line_header)
        {
            var tmp = new Dictionary<String, String>();

            foreach (var line in line_header)
            {
                var sep_ndx = line.IndexOf(':');

                if (sep_ndx > -1)
                {
                    var key = line.Substring(0, sep_ndx).Trim();
                    var value = line.Substring(sep_ndx + 1).Trim();

                    tmp.Add(key.ToLower(), value.ToLower());
                } else
                {
                    var space_ndx_0 = line.IndexOf(' ');
                    var space_ndx_1 = line.IndexOf(' ', space_ndx_0);

                    if (space_ndx_0 > -1 && space_ndx_1 > -1)
                    {
                        var parts = line.Split(' ');

                        tmp["$method"] = parts[0];
                        tmp["$url"] = parts[1];
                        tmp["$version"] = parts[2];
                    }
                }
            }

            return tmp;
        }

        /// <summary>
        /// The function responsible for handling each request. This function can be implemented by subclassing
        /// of this class and using override. The function is provided with the request header, request body, and
        /// the response encoder which allows setting of headers and data if any.
        /// </summary>
        /// <param name="header"></param>
        /// <param name="body"></param>
        /// <param name="encoder"></param>
        /// <returns></returns>
        public virtual async Task HandleRequest(Dictionary<String, String> header, Stream body, ProxyHTTPEncoder encoder)
        {
            var outheader = new Dictionary<String, String>();

            outheader.Add("$response_code", "200");
            outheader.Add("$response_text", "OK");

            Console.WriteLine("Sending response header now.");

            await encoder.WriteHeader(outheader);

            Console.WriteLine("Sending response body now.");

            MemoryStream ms = new MemoryStream();

            byte[] something = Encoding.UTF8.GetBytes("hello world\n");

            ms.Write(something, 0, something.Length);
            ms.Write(something, 0, something.Length);
            ms.Write(something, 0, something.Length);
            ms.Write(something, 0, something.Length);
            ms.Write(something, 0, something.Length);

            ms.Position = 0;

            //await encoder.BodyWriteSingleChunk("response test body");
            await encoder.BodyWriteStream(ms);

            Console.WriteLine("Response has been sent.");
        }

        public async Task Handle()
        {
            var q = new Queue<ProxyHTTPEncoder>();

            var qchanged = new AsyncManualResetEvent();

            // This task watches the `q` queue and starts and removes
            // proxy objects representing the HTTP encoder. Each request
            // gets its own proxy object and all methods on the proxy either
            // block or buffer until the proxy object becomes ready.
#pragma warning disable 4014
            Task.Run(async () =>
            {
                while (true)
                {
                    ProxyHTTPEncoder phe;

                    Console.WriteLine("waiting on qchanged");
                    await qchanged.WaitAsync();

                    Console.WriteLine("reset qchanged");
                    qchanged.Reset();

                    // Only lock long enough to get the first item.
                    lock (q)
                    {
                        phe = q.Peek();
                    }

                    if (phe == null)
                    {
                        // The exit signal.
                        Console.WriteLine("peeked null; now exiting");
                        break;
                    }

                    // Signal this object that it is ready to do work.
                    phe.ready.Set();
                    Console.WriteLine("signaling phe as ready");

                    // Wait until it is done.
                    await phe.done.WaitAsync();

                    Console.WriteLine("phe is done");

                    phe.Death();

                    // Remove it, and signal the next to go.
                    q.Dequeue();
                    Console.WriteLine("phe dequeued");
                }
            });
#pragma warning restore 4014

            while (true)
            {
                Console.WriteLine("###### Handling the next request. ######");
                // Need a way for this to block (await) until the body has been completely
                // read. This logic could be implemented inside the decoder.
                var line_header = await decoder.ReadHeader();

                Console.WriteLine("Header to dictionary.");

                var header = LineHeaderToDictionary(line_header);

                if (header == null)
                {
                    Console.WriteLine("Connection has been lost.");
                    break;
                }

                Stream body;

                Console.WriteLine("Got header.");

                Task body_reading_task;

                if (header.ContainsKey("content-length"))
                {
                    Console.WriteLine("Got content-length.");
                    // Content length specified body follows.
                    long content_length = (long)Convert.ToUInt32(header["content-length"]);

                    (body, body_reading_task) = await decoder.ReadBody(HTTPDecoderBodyType.ContentLength(content_length));
                }
                else if (header.ContainsKey("transfer-encoding"))
                {
                    Console.WriteLine("Got chunked.");
                    // Chunked encoded body follows.
                    (body, body_reading_task) = await decoder.ReadBody(HTTPDecoderBodyType.ChunkedEncoding());
                }
                else
                {
                    Console.WriteLine("Got no body.");
                    // No body follows. (Not using await to allow pipelining.)
                    (body, body_reading_task) = await decoder.ReadBody(HTTPDecoderBodyType.NoBody());
                }

                bool close_connection = true;

                if (header.ContainsKey("connection") && !header["connection"].ToLower().Equals("close"))
                {
                    close_connection = false;
                }

                var phe = new ProxyHTTPEncoder(encoder, close_connection);

                q.Enqueue(phe);

                qchanged.Set();

                Console.WriteLine("Allowing handling of request.");

                await HandleRequest(header, body, phe);

                // We must wait until both the HandleRequest returns and
                // that the task, if any, which is reading the body also
                // completes.
                if (body_reading_task != null)
                {
                    // Do not block while waiting.
                    await body_reading_task;
                }
            }
        }
    }
}

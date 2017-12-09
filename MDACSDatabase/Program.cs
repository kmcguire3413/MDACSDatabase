using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using MDACS.API;
using System.Text;
using System.IO;
using static MDACS.API.Auth;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Security.Cryptography.X509Certificates;
using System.Net.Sockets;
using System.Threading;
using System.Net.Security;

namespace MDACS.Server
{
    /*
    class Item
    {
        public String datatype;
        public String datestr;
        public String devicestr;
        public String timestr;
        public ulong datasize;
        public String node;
        public String security_id;
        public String note;
    }

    class Server: IHttpModule
    {
        private Dictionary<String, Item> items;

        public Server()
        {
            items = new Dictionary<string, Item>();

            var item = new Item();

            item.datatype = "a";
            item.datestr = "b";
            item.timestr = "c";

            items.Add("test", item);
        }

        class AuthenticationException: Exception
        {

        }

        public async Task<AuthCheckResponse> ReadMessageFromStreamAndAuthenticate(int max_size, Stream input_stream)
        {
            var buf = new byte[1024 * 32];
            int pos = 0;

            var a = input_stream.CanRead;
            var b = input_stream.CanTimeout;

            while (pos < buf.Length)
            {
                var cnt = await input_stream.ReadAsync(buf, pos, buf.Length - pos);
                if (cnt < 1)
                {
                    break;
                }
                pos += cnt;
            }

            var resp = await MDACS.API.Auth.AuthenticateMessageAsync(
                "https://epdmdacs.kmcg3413.net:34002",
                Encoding.UTF8.GetString(buf)
            );

            return resp;
        }

        public async Task WriteMessageToStream(Stream output_stream, String out_string)
        {
            var out_buffer = Encoding.UTF8.GetBytes(out_string);
            await output_stream.WriteAsync(out_buffer, 0, out_buffer.Length);
        }

        private class HandleDataReply
        {
            public Item[] data;
        }

        private async void HandleData(IHttpContext context)
        {
            var req = context.Request;

            var auth_resp = await ReadMessageFromStreamAndAuthenticate(1024 * 16, req.Body);

            req.Body.Close();

            if (!auth_resp.success)
            {
                throw new AuthenticationException();
            }

            var reply = new HandleDataReply();
            reply.data = new Item[this.items.Count];

            lock (this.items)
            {
                int x = 0;

                foreach (var pair in this.items)
                {
                    reply.data[x++] = pair.Value;
                }
            }

            await WriteMessageToStream(
                context.Response.GetResponseStream(),
                JsonConvert.SerializeObject(reply)
            );

            context.Response.GetResponseStream().Close();
        }

        public async Task<bool> HandleAsync(IHttpContext context)
        {
            var req = context.Request;
            var url = req.Path;

            switch (url)
            {
                case "/device-config":
                    break;
                case "/upload":
                    break;
                case "/download":
                    break;
                case "/data":
                    HandleData(context);
                    break;
                case "/delete":
                    break;
                case "/commit":
                    break;
                case "/commitset":
                    break;
                case "/update-alerts":
                    break;
                case "/enumerate-configurations":
                    break;
                case "/commit-configuration":
                    break;
                case "/commit_batch_single_ops":
                    break;
            }

            return true;
        }
    }
    */

    class ProxyHTTPEncoder
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
                } else
                {
                    header.Add("connection", "close");
                }
            } else
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
            byte[] buf = new byte[1024 * 4];
            bool first_chunk = true;

            Console.WriteLine("BodyWriteStreamInternal: Now running.");

            do
            {
                var cnt = await inpstream.ReadAsync(buf, 0, buf.Length);

                if (cnt < 1)
                {
                    Console.WriteLine("BodyWriteStreamInternal: EOS");
                    break;
                }
                
                if (first_chunk)
                {
                    Console.WriteLine("BodyWriteStreamInternal: First chunk.");
                    await this.encoder.BodyWriteFirstChunk(buf, 0, cnt);
                    first_chunk = false;
                } else
                {
                    Console.WriteLine("BodyWriteStreamInternal: Next chunk.");
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
        public async Task BodyWriteStream(Stream inpstream)
        {
            await this.ready.WaitAsync();

            // Control needs to return to the caller. Do not `await` the result of this task.
            Task.Run(() => BodyWriteStreamInternal(inpstream));
        }
    }

    class HTTPClient
    {
        private HTTPDecoder decoder;
        private HTTPEncoder encoder;

        public HTTPClient(HTTPDecoder decoder, HTTPEncoder encoder)
        {
            this.decoder = decoder;
            this.encoder = encoder;
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
                }
            }

            return tmp;
        }

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

                if (header.ContainsKey("content-length"))
                {
                    Console.WriteLine("Got content-length.");
                    // Content length specified body follows.
                    long content_length = (long)Convert.ToUInt32(header["content-length"]);

                    body = await decoder.ReadBody(HTTPDecoderBodyType.ContentLength(content_length));
                } else if (header.ContainsKey("transfer-encoding"))
                {
                    Console.WriteLine("Got chunked.");
                    // Chunked encoded body follows.
                    body = await decoder.ReadBody(HTTPDecoderBodyType.ChunkedEncoding());
                } else {
                    Console.WriteLine("Got no body.");
                    // No body follows. (Not using await to allow pipelining.)
                    body = await decoder.ReadBody(HTTPDecoderBodyType.NoBody());
                }

                bool close_connection = true;

                if (header.ContainsKey("connection") && !header["connection"].ToLower().Equals("close"))
                {
                    close_connection = false;
                }

                // Currently, pipelining is not enabled.

                var phe = new ProxyHTTPEncoder(encoder, close_connection);

                q.Enqueue(phe);

                qchanged.Set();

                Console.WriteLine("Allowing handling of request.");
                await HandleRequest(header, body, phe);
                
            }
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            TcpListener listener = new TcpListener(IPAddress.Any, 34001);

            listener.Start();

            var x509 = new X509Certificate2("c:\\users\\kmcgu\\Desktop\\test.pfx", "hello");

            while (true)
            {
                var client = listener.AcceptTcpClient();
                var ssl_sock = new SslStream(client.GetStream(), false);

                Task.Run(async () =>
                {
                    Console.WriteLine("Authenticating as server through SSL/TLS.");

                    await ssl_sock.AuthenticateAsServerAsync(x509);

                    var http_decoder = new HTTPDecoder(ssl_sock);
                    var http_encoder = new HTTPEncoder(ssl_sock);
                    var http_client = new HTTPClient(http_decoder, http_encoder);

                    Console.WriteLine("Handling client.");
                    await http_client.Handle();
                });
            }

            /*
            var config = new Ceen.Httpd.ServerConfig();

            // Please do not let me forget this convulted retarded sequence to get from PEM to PFX with the private key.
            // openssl crl2pkcs7 -nocrl -inkey privkey.pem -certfile fullchain.pem -out test.p7b
            // openssl pkcs7 -print_certs -in test.p7b -out test.cer
            // openssl pkcs12 -export -in test.cer -inkey privkey.pem -out test.pfx -nodes
            // THEN... for Windows, at least, import into cert store, then export with private key and password.
            // FINALLY... use the key now and make sure its X509Certificate2.. notice the 2 on the end? Yep.
            var x509 = new X509Certificate2("c:\\users\\kmcgu\\Desktop\\test.pfx", "hello");

            config.SSLCertificate = x509;

            var server = new Server();

            config.AddRoute(server);
        
            var listener = Ceen.Httpd.HttpServer.ListenAsync(
                new IPEndPoint(IPAddress.Any, 34001),
                true,
                config
            );

            listener.Wait();
            */

            /*
            var listener = new System.Net.HttpListener();
            var server = new Server();

            listener.Start();

            listener.Prefixes.Add("https://127.0.0.1:34001/");

            while (true)
            {
                var context = listener.GetContext();
                //Task.Run(async () =>
                //{

                server.HandleContext(context);
                
                //});
            }
            */
        }
    }
}

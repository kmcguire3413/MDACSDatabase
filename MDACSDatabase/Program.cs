#define DOUBLE_ENDED_STREAM_DEBUG

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
using System.Security.Cryptography;
using MDACS.Server;
using static MDACS.Logger;

namespace MDACS.Database
{
    public struct Item
    {
        public String security_id;
        public String node;
        public double duration;
        public double metatime;
        public String fqpath;
        public String userstr;
        public String timestr;
        public String datestr;
        public String devicestr;
        public String datatype;
        public ulong datasize;
        public String note;
        public String state;
        public String[][] versions;

        public static String Serialize(Item item)
        {
            return JsonConvert.SerializeObject(item);
        }

        public static Item Deserialize(String input)
        {
            return JsonConvert.DeserializeObject<Item>(input);
        }
    }

    /// <summary>
    /// A type of exception that all exceptions thrown by this program must derive from. If any exception caught must only be rethrown
    /// if it is embedded as the `caught_exception` property of this class. This can be done by calling the appropriate constructor.
    /// </summary>
    class ProgramException: Exception
    {
        public Exception caught_exception { get; }

        public ProgramException(String msg) : base(msg)
        {

        }

        public ProgramException() : base ()
        {

        }

        public ProgramException(String msg, Exception caught_exception) : base(msg)
        {
            this.caught_exception = caught_exception;
        }
    }

    class JournalHashException: ProgramException
    {
    }

    class UnauthorizedException: ProgramException
    {

    }

    class HTTPClient3: HTTPClient2
    {
        private ServerHandler shandler;

        public HTTPClient3(IHTTPServerHandler shandler, HTTPDecoder decoder, HTTPEncoder encoder) : base(shandler, decoder, encoder)
        {
            this.shandler = shandler as ServerHandler;
        }

        class AuthenticationException : ProgramException
        {

        }

        class InvalidArgumentException : ProgramException
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
                shandler.auth_url,
                Encoding.UTF8.GetString(buf, 0, pos)
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

        private class HandleCommitSetRequest
        {
            public String security_id;
            public Dictionary<String, String> meta;
        }

        private class HandleCommitSetResponse
        {
            public bool success;
        }

        private class HandleBatchSingleOpsResponse
        {
            public bool success;
            public String[][] failed;
        }

        private class HandleBatchSingleOpsRequest
        {
            public String[][] ops;
        }

        private bool CanUserModifyItem(User user, Item item)
        {
            if (user.admin)
            {
                return true;
            }

            if (item.userstr.IndexOf(user.userfiler) == 0)
            {
                return true;
            }

            return false;
        }

        private bool CanUserSeeItem(User user, Item item)
        {
            return CanUserModifyItem(user, item);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="request"></param>
        /// <param name="body"></param>
        /// <param name="encoder"></param>
        /// <exception cref="UnauthorizedException">User has insufficient priviledges to modify the item.</exception>
        /// <exception cref="AuthenticationException">User is not valid for access of any type.</exception>
        /// <exception cref="InvalidArgumentException">One of the arguments was not correct or the reason for failure.</exception>
        /// <exception cref="ProgramException">Anything properly handled but needs handling for acknowlegement purposes.</exception>
        /// <exception cref="Exception">Anything else could result in instability.</exception>
        private async void HandleCommitSet(HTTPRequest request, Stream body, ProxyHTTPEncoder encoder)
        {
            var auth_resp = await ReadMessageFromStreamAndAuthenticate(1024 * 16, body);

            if (!auth_resp.success)
            {
                throw new AuthenticationException();
            }

            var sreq = JsonConvert.DeserializeObject<HandleCommitSetRequest>(auth_resp.payload);

            Monitor.Enter(shandler.items);

            Item item;

            try
            {
                if (!shandler.items.ContainsKey(sreq.security_id))
                {
                    throw new InvalidArgumentException();
                }

                item = shandler.items[sreq.security_id];
            }
            catch (Exception ex)
            {
                throw new ProgramException("Unidentified problem.", ex);
            }
            finally
            {
                Monitor.Exit(shandler.items);
            }

            if (!CanUserModifyItem(auth_resp.user, item))
            {
                throw new UnauthorizedException();
            }

            try
            {
                foreach (var pair in sreq.meta)
                {
                    // Reflection simplified coding time at the expense of performance.
                    item.GetType().GetProperty(pair.Key).SetValue(item, pair.Value);
                    Logger.LogLine(String.Format("Set property {0} for item to {1}.", pair.Key, pair.Value));
                }

                shandler.items[sreq.security_id] = item;

                if (!await shandler.WriteItemToJournal(item))
                {
                    throw new ProgramException("Unable to write to the journal.");
                }
            }
            catch (Exception ex)
            {
                throw new ProgramException("Caught inner exception when setting item with commit set command.", ex);
            }

            // Success.
            await encoder.WriteQuickHeader(200, "OK");
            await encoder.BodyWriteSingleChunk("{ \"success\": true }");
        }

        private async void HandleData(HTTPRequest request, Stream body, ProxyHTTPEncoder encoder)
        {
            var auth_resp = await ReadMessageFromStreamAndAuthenticate(1024 * 16, body);

            if (!auth_resp.success)
            {
                throw new AuthenticationException();
            }

            var reply = new HandleDataReply();

            reply.data = new Item[shandler.items.Count];

            lock (shandler.items)
            {
                int x = 0;

                foreach (var pair in shandler.items)
                {
                    // PRIVCHECK MARK
                    if (
                        CanUserSeeItem(auth_resp.user, pair.Value)
                    )
                    {
                        reply.data[x++] = pair.Value;
                    }
                }
            }

            using (var de_stream = new DoubleEndedStream())
            {
                await encoder.WriteQuickHeader(200, "OK");
                var tmp = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(reply));
                await encoder.BodyWriteStream(de_stream);
                await de_stream.WriteAsync(tmp, 0, tmp.Length);
            }
        }

        class HandleEnumerateConfigurationsResponse
        {
            public bool success;
            public Dictionary<String, String> configs;
        }

        private async void HandleEnumerateConfigurations(HTTPRequest request, Stream body, ProxyHTTPEncoder encoder)
        {
            var auth = await ReadMessageFromStreamAndAuthenticate(1024 * 16, body);

            if (!auth.success)
            {
                throw new AuthenticationException();
            }

            var resp = new HandleEnumerateConfigurationsResponse();

            resp.configs = new Dictionary<string, string>();

            try
            {
                foreach (var node in Directory.EnumerateFiles(shandler.data_path))
                {
                    var fnode = Path.Combine(shandler.data_path, node);

                    if (fnode.StartsWith("config_"))
                    {
                        var fd = File.OpenRead(fnode);

                        var buf = new byte[fd.Length];

                        int cnt = 0;

                        while (cnt < buf.Length)
                        {
                            cnt += await fd.ReadAsync(buf, cnt, buf.Length - cnt);
                        }

                        var buf_text = Encoding.UTF8.GetString(buf);

                        resp.configs[fnode.Substring(fnode.IndexOf("_") + 1)] = buf_text;

                        fd.Dispose();
                    }
                }
            } catch (Exception ex)
            {
                throw new ProgramException("Error happened during enumeration of configurations.", ex);
            }

            using (var de_stream = new DoubleEndedStream())
            {
                await encoder.WriteQuickHeader(200, "OK");
                var tmp = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(resp));
                await encoder.BodyWriteStream(de_stream);
                await de_stream.WriteAsync(tmp, 0, tmp.Length);
            }
        }

        class HandleDownloadRequest
        {
            public String security_id;
        }

        private async void HandleDownload(HTTPRequest request, Stream body, ProxyHTTPEncoder encoder)
        {
            // This URL route was never implemented with user security. The security ID itself is the security, but that
            // could be revised.
            //var auth = await ReadMessageFromStreamAndAuthenticate(1024 * 16, body);
            //if (!auth.success)
            //{
            //    throw new AuthenticationException();
            //}

            HandleDownloadRequest req = null;

            String download_sid;

            if (request.query_string.Length > 0)
            {
                download_sid = request.query_string;
            } else
            {
                //req = JsonConvert.DeserializeObject<HandleDownloadRequest>(auth.payload);
                //download_sid = req.security_id;
                throw new InvalidArgumentException();
            }

            Item item;

            lock (shandler.items)
            {
                if (!shandler.items.ContainsKey(download_sid))
                {
                    throw new InvalidArgumentException();
                }

                item = shandler.items[download_sid];
            }

            var item_data_path = Path.Combine(shandler.data_path, item.node);

            var fd = File.OpenRead(item_data_path);

            ulong offset_start = 0;
            // How can a stream be negative? Could this stream ever be negative? What purpose does it suit?
            ulong offset_size = (ulong)fd.Length;
            ulong total_size = (ulong)fd.Length;

            String response_code = "200";

            if (request.internal_headers.ContainsKey("range"))
            {
                var range_str = request.internal_headers["range"];

                var eqndx = range_str.IndexOf("=");

                if (eqndx > -1)
                {
                    var range_sub_str = range_str.Substring(eqndx + 1).Trim();
                    var range_nums_strs = range_sub_str.Split("-");

                    if (range_nums_strs.Length > 1)
                    {
                        offset_start = ulong.Parse(range_nums_strs[0]);

                        if (range_nums_strs[1].Equals(""))
                        {
                            offset_size = (ulong)fd.Length - offset_start;
                        }
                        else
                        {
                            offset_size = ulong.Parse(range_nums_strs[1]) - offset_start + 1;
                        }
                        response_code = "206";
                    }
                }
            }

            checked
            {
                fd.Seek((long)offset_start, SeekOrigin.Begin);
            }

            fd.Seek((long)offset_start, SeekOrigin.Begin);

            String mime_type = null;

            switch (item.datatype)
            {
                case "mp4":
                    mime_type = "video/mp4";
                    break;
                case "jpg":
                    mime_type = "image/jpeg";
                    break;
            }

            using (var de_stream = new LimitedStream(fd, offset_size))
            {
                var header = new Dictionary<String, String>();

                header.Add("$response_code", response_code);
                header.Add("$response_text", "Partial Content");
                header.Add("content-disposition", 
                    String.Format("inline; filename=\"{0}_{1}_{2}_{3}.{4}\"",
                        item.datestr,
                        item.userstr,
                        item.devicestr,
                        item.timestr,
                        item.datatype
                    )
                );
                header.Add("accept-ranges", "bytes");
                header.Add("content-range", 
                    String.Format("bytes {0}-{1}/{2}", 
                        offset_start,
                        offset_size + offset_start - 1,
                        total_size
                    )
                );

                if (mime_type != null)
                {
                    header.Add("content-type", mime_type);
                }

                await encoder.WriteHeader(header);
                await encoder.BodyWriteStream(de_stream);
            }
        }

        private async void HandleBatchSingleOps(HTTPRequest request, Stream body, ProxyHTTPEncoder encoder)
        {
            var auth = await ReadMessageFromStreamAndAuthenticate(1024 * 16, body);

            if (!auth.success)
            {
                throw new AuthenticationException();
            }

            var req = JsonConvert.DeserializeObject<HandleBatchSingleOpsRequest>(auth.payload);

            List<String[]> failed = new List<String[]>();

            var tasks = new List<Task<bool>>();

            foreach (var op in req.ops)
            {
                if (op.Length < 3)
                {
                    continue;
                }

                var sid = op[0];
                var key = op[1];
                var val = op[2];

                lock (shandler.items)
                {
                    if (shandler.items.ContainsKey(sid))
                    {
                        try
                        {
                            shandler.items[sid].GetType().GetProperty(key).SetValue(shandler.items[sid], val);
                            tasks.Add(shandler.WriteItemToJournal(shandler.items[sid]));
                        } catch (Exception ex)
                        {
                            failed.Add(new String[] { sid, key, val, ex.ToString() });
                        }
                    }
                }
            }

            Task.WaitAll(tasks.ToArray());

            using (var de_stream = new DoubleEndedStream())
            {
                var resp = new HandleBatchSingleOpsResponse();

                resp.success = true;
                resp.failed = failed.ToArray();

                await encoder.WriteQuickHeader(200, "OK");
                var tmp = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(resp));
                await encoder.BodyWriteStream(de_stream);
                await de_stream.WriteAsync(tmp, 0, tmp.Length);
            }
        }

        public override async Task HandleRequest2(HTTPRequest request, Stream body, ProxyHTTPEncoder encoder)
        {
            Logger.LogLine(String.Format("url={0}", request.url));

            switch (request.url_absolute)
            {
                case "/device-config":
                    // ###
                    break;
                case "/upload":
                    // ###
                    break;
                case "/download":
                    HandleDownload(request, body, encoder);
                    break;
                case "/data":
                    HandleData(request, body, encoder);
                    break;
                case "/delete":
                    // ### 2
                    break;
                case "/commit":
                    break;
                case "/commitset":
                    HandleCommitSet(request, body, encoder);
                    break;
                case "/enumerate-configurations":
                    HandleEnumerateConfigurations(request, body, encoder);
                    break;
                case "/commit-configuration":
                    // ###
                    break;
                case "/commit_batch_single_ops":
                    HandleBatchSingleOps(request, body, encoder);
                    break;
            }
        }
    }

    class ServerHandler : IHTTPServerHandler
    {
        public Dictionary<String, Item> items;
        public String auth_url;
        public String db_url;
        public String metajournal_path;
        public String data_path;

        public ServerHandler(
            String metajournal_path, 
            String data_path,
            String auth_url,
            String db_url
        )
        {
            this.data_path = data_path;
            this.auth_url = auth_url;
            this.db_url = db_url;
            this.metajournal_path = metajournal_path;

            items = new Dictionary<string, Item>();

            var mj = File.OpenText(metajournal_path);

            var hasher = MD5.Create();

            while (!mj.EndOfStream)
            {
                var line = mj.ReadLine();

                var colon_ndx = line.IndexOf(':');

                var hash = line.Substring(0, colon_ndx);
                var meta = line.Substring(colon_ndx + 1).TrimEnd();

                var correct_hash = BitConverter.ToString(
                    hasher.ComputeHash(Encoding.UTF8.GetBytes(meta))
                ).Replace("-", "").ToLower();

                if (hash != correct_hash)
                {
                    mj.Dispose();
                    throw new JournalHashException();
                }

                var metaitem = Item.Deserialize(meta);

                if (items.ContainsKey(metaitem.security_id))
                {
                    // Overwrite existing entries.
                    items[metaitem.security_id] = metaitem;
                }
                else
                {
                    // Add new entries.
                    items.Add(metaitem.security_id, metaitem);
                }
            }

            mj.Dispose();
        }

        public async Task<bool> WriteItemToJournal(Item item)
        {
            using (var mj = File.OpenWrite(this.metajournal_path))
            {
                var meta = Item.Serialize(item);
                var hasher = MD5.Create();

                var hash = BitConverter.ToString(
                    hasher.ComputeHash(
                        Encoding.UTF8.GetBytes(meta)
                    )
                ).Replace("-", "");

                byte[] line_bytes = Encoding.UTF8.GetBytes(String.Format("{0}:{1}\n", hash, meta));

                await mj.WriteAsync(line_bytes, 0, line_bytes.Length);
            }

            return true;
        }

        public override HTTPClient CreateClient(IHTTPServerHandler shandler, HTTPDecoder decoder, HTTPEncoder encoder)
        {
            return new HTTPClient3(shandler, decoder, encoder);
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var handler = new ServerHandler(
                "c:\\users\\kmcgu\\Desktop\\metajournal-2017-12-10",
                @"Y:\camerasys_secure\data",
                "https://epdmdacs.kmcg3413.net:34002",
                "https://epdmdacs.kmcg3413.net:34001"
            );

            var server = new HTTPServer<ServerHandler>(handler, "c:\\users\\kmcgu\\Desktop\\test.pfx", "hello");
            server.Start();

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

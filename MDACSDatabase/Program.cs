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
using static MDACS.Server.HTTPClient2;

namespace MDACS.Database
{
    public class Item
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
    internal class ProgramException: Exception
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

    internal class JournalHashException: ProgramException
    {
    }

    internal class UnauthorizedException: ProgramException
    {

    }

    internal class InvalidArgumentException : ProgramException
    {

    }


    internal class Helpers
    {
        public static async Task<AuthCheckResponse> ReadMessageFromStreamAndAuthenticate(ServerHandler shandler, int max_size, Stream input_stream)
        {
            var buf = new byte[1024 * 32];
            int pos = 0;

            var a = input_stream.CanRead;
            var b = input_stream.CanTimeout;

            Console.WriteLine("Reading authenticated payload.");

            while (pos < buf.Length)
            {
                var cnt = await input_stream.ReadAsync(buf, pos, buf.Length - pos);

                if (cnt < 1)
                {
                    break;
                }

                pos += cnt;
            }

            Console.WriteLine("Done reading authenticated payload.");

            var buf_utf8_string = Encoding.UTF8.GetString(buf, 0, pos);

            Console.WriteLine(buf_utf8_string);

            var resp = await MDACS.API.Auth.AuthenticateMessageAsync(
                shandler.auth_url,
                buf_utf8_string
            );

            Console.WriteLine("Handing back result from authenticated payload.");

            return resp;
        }

        public static bool CanUserModifyItem(User user, Item item)
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

        public static bool CanUserSeeItem(User user, Item item)
        {
            return CanUserModifyItem(user, item);
        }

        public static async Task WriteMessageToStream(Stream output_stream, String out_string)
        {
            var out_buffer = Encoding.UTF8.GetBytes(out_string);
            await output_stream.WriteAsync(out_buffer, 0, out_buffer.Length);
        }
    }

    internal delegate Task HTTPClient3Handler(ServerHandler shandler, HTTPRequest request, Stream body, ProxyHTTPEncoder encoder);

    internal class HTTPClient3 : HTTPClient2
    {
        private ServerHandler shandler;
        private Dictionary<String, HTTPClient3Handler> handlers;

        public HTTPClient3(
            IHTTPServerHandler shandler, 
            HTTPDecoder decoder,
            HTTPEncoder encoder,
            Dictionary<String, HTTPClient3Handler> handlers
        ) : base(shandler, decoder, encoder)
        {
            this.shandler = shandler as ServerHandler;
            this.handlers = handlers;
        }

        /// <summary>
        /// The entry point for route handling. Provides common error response from exception propogation.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="body"></param>
        /// <param name="encoder"></param>
        /// <returns>Asynchronous task object.</returns>
        public override async Task HandleRequest2(HTTPRequest request, Stream body, ProxyHTTPEncoder encoder)
        {
            try
            {
                Logger.LogLine(String.Format("url={0}", request.url));

                if (!this.handlers.ContainsKey(request.url_absolute))
                {
                    throw new NotImplementedException();
                }

                await this.handlers[request.url_absolute](this.shandler, request, body, encoder);
            } catch (Exception ex)
            {
                Console.WriteLine("==== EXCEPTION ====");
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }
    }

    internal class ServerHandler : IHTTPServerHandler
    {
        public Dictionary<String, Item> items;
        public String auth_url;
        public String metajournal_path;
        public String data_path;
        public String config_path;

        public ServerHandler(
            String metajournal_path,
            String data_path,
            String config_path,
            String auth_url
        )
        {
            this.data_path = data_path;
            this.config_path = config_path;
            this.auth_url = auth_url;
            this.metajournal_path = metajournal_path;

            items = new Dictionary<string, Item>();

            var mj = File.OpenText(metajournal_path);

            var hasher = MD5.Create();

            Console.WriteLine("Reading journal into memory.");

            while (!mj.EndOfStream)
            {
                var line = mj.ReadLine();

                var colon_ndx = line.IndexOf(':');

                Item metaitem;
                try
                {
                    var hash = line.Substring(0, colon_ndx);
                    var meta = line.Substring(colon_ndx + 1).TrimEnd();

                    var correct_hash = BitConverter.ToString(
                        hasher.ComputeHash(Encoding.UTF8.GetBytes(meta))
                    ).Replace("-", "").ToLower();

                    if (hash != correct_hash)
                    {
                        //mj.Dispose();
                        throw new JournalHashException();
                    }

                    metaitem = Item.Deserialize(meta);
                } catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    continue;
                }

                var sid = metaitem.security_id;

                if (sid.Length != (512 / 8 * 2))
                {
                    // Throw out entries with a bad sid.
                    continue;
                }

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

            Console.WriteLine("Done reading journal into memory.");

            mj.Dispose();
        }

        public async Task<bool> WriteItemToJournal(Item item)
        {
            using (var mj = File.Open(this.metajournal_path, FileMode.Append))
            {
                var meta = Item.Serialize(item);
                var hasher = MD5.Create();

                var hash = BitConverter.ToString(
                    hasher.ComputeHash(
                        Encoding.UTF8.GetBytes(meta)
                    )
                ).Replace("-", "").ToLower();

                byte[] line_bytes = Encoding.UTF8.GetBytes(String.Format("{0}:{1}\n", hash, meta));

                await mj.WriteAsync(line_bytes, 0, line_bytes.Length);
            }

            if (items.ContainsKey(item.security_id))
            {
                items[item.security_id] = item;
            }
            else
            {
                items.Add(item.security_id, item);
            }

            return true;
        }

        public override HTTPClient CreateClient(IHTTPServerHandler shandler, HTTPDecoder decoder, HTTPEncoder encoder)
        {
            var handlers = new Dictionary<String, HTTPClient3Handler>();

            handlers.Add("/upload", HandleUpload.Action);
            handlers.Add("/device-config", HandleDeviceConfig.Action);
            handlers.Add("/commit_batch_single_ops", HandleBatchSingleOps.Action);
            handlers.Add("/download", HandleDownload.Action);
            handlers.Add("/enumerate-configurations", HandleEnumerateConfigurations.Action);
            handlers.Add("/data", HandleData.Action);
            handlers.Add("/commitset", HandleCommitSet.Action);
            handlers.Add("/commit-configuration", HandleCommitConfiguration.Action);
            handlers.Add("/delete", HandleDelete.Action);

            // missing /delete
            // missing /commit
            //      Prefer /commitset and /commit-batch-single-ops due to atomic compatibility.
            return new HTTPClient3(
                shandler: shandler, 
                decoder: decoder, 
                encoder: encoder,
                handlers: handlers
            );
        }
    }

    struct ProgramConfig
    {
        public String metajournal_path;
        public String data_path;
        public String config_path;
        public String auth_url;
        public ushort port;
    }

    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Provide path or file that contains the JSON configuration. If file does not exit then default one will be created.");
                return;
            }

            if (!File.Exists(args[0]))
            {
                ProgramConfig defcfg = new ProgramConfig {
                    metajournal_path = "The file path to the metadata journal.",
                    data_path = "The path to the directory containing the data files backing the journal.",
                    config_path = "The path to the directory holding device configuration files.",
                    auth_url = "The HTTP or HTTPS URL to the authentication service.",
                    port = 34001,
                };

                var defcfgfp = File.CreateText(args[0]);
                defcfgfp.Write(JsonConvert.SerializeObject(defcfg, Formatting.Indented));
                defcfgfp.Dispose();

                Console.WriteLine("Default configuration created at location specified.");
                return;
            }

            var cfgfp = File.OpenText(args[0]);

            var cfg = JsonConvert.DeserializeObject<ProgramConfig>(cfgfp.ReadToEnd());

            cfgfp.Dispose();

            var handler = new ServerHandler(
                metajournal_path: cfg.metajournal_path,
                data_path: cfg.data_path,
                config_path: cfg.config_path,
                auth_url: cfg.auth_url
            );

            var server = new HTTPServer<ServerHandler>(handler, "test.pfx", "hello");
            server.Start(cfg.port).Wait();

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

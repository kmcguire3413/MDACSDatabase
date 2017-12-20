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
using static MDACS.Server.HTTPClient2;

namespace MDACS.App
{
    /// <summary>
    /// A type of exception that all exceptions thrown by this program must derive from. If any exception caught must only be rethrown
    /// if it is embedded as the `caught_exception` property of this class. This can be done by calling the appropriate constructor.
    /// </summary>
    internal class ProgramException : Exception
    {
        public Exception caught_exception { get; }

        public ProgramException(String msg) : base(msg)
        {

        }

        public ProgramException() : base()
        {

        }

        public ProgramException(String msg, Exception caught_exception) : base(msg)
        {
            this.caught_exception = caught_exception;
        }
    }

    internal class JournalHashException : ProgramException
    {
    }

    internal class UnauthorizedException : ProgramException
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

            var resp = await MDACS.API.Auth.AuthenticateMessageAsync(
                shandler.auth_url,
                Encoding.UTF8.GetString(buf, 0, pos)
            );

            Console.WriteLine("Handing back result from authenticated payload.");

            return resp;
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
                Console.WriteLine(String.Format("url={0}", request.url));

                if (!this.handlers.ContainsKey(request.url_absolute))
                {
                    throw new NotImplementedException();
                }

                await this.handlers[request.url_absolute](this.shandler, request, body, encoder);
            }
            catch (Exception ex)
            {
                Console.WriteLine("==== EXCEPTION ====");
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }
    }

    static class Handlers
    {
        public static async Task Index(ServerHandler shandler, HTTPRequest request, Stream body, ProxyHTTPEncoder encoder)
        {

        }

        public static async Task Viewer(ServerHandler shandler, HTTPRequest request, Stream body, ProxyHTTPEncoder encoder)
        {

        }

        public static async Task Utility(ServerHandler shandler, HTTPRequest request, Stream body, ProxyHTTPEncoder encoder)
        {

        }
    }

    internal class ServerHandler : IHTTPServerHandler
    {
        public String auth_url;
        public String db_url;

        public ServerHandler(
            String auth_url,
            String db_url
        )
        {
            this.auth_url = auth_url;
            this.db_url = db_url;
        }

        public override HTTPClient CreateClient(IHTTPServerHandler shandler, HTTPDecoder decoder, HTTPEncoder encoder)
        {
            var handlers = new Dictionary<String, HTTPClient3Handler>();

            handlers.Add("/", Handlers.Index);
            /*handlers.Add("/enumerate-configurations", Handlers.EnumerateConfigurations);
            handlers.Add("/viewer", Handlers.Viewer);
            handlers.Add("/challenge", Handlers.Challenge);
            handlers.Add("/utility", Handlers.Utility);
            handlers.Add("/commitset", Handlers.CommitSet);
            handlers.Add("/commit_batch_single_ops", Handlers.CommitBatchSingleOps);
            handlers.Add("/data", Handlers.Data);
            handlers.Add("/download", Handlers.Download);*/

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
        public String auth_url;
        public String db_url;
    }

    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Provide path or file that contains the JSON configuration. If file does not exit then default one will be created.");
                return;
            }

            if (!File.Exists(args[1]))
            {
                ProgramConfig defcfg = new ProgramConfig
                {
                    auth_url = "The HTTP or HTTPS URL to the authentication service.",
                    db_url = "The HTTP or HTTPS URL to the database service."
                };

                var defcfgfp = File.CreateText(args[1]);
                defcfgfp.Write(JsonConvert.SerializeObject(defcfg, Formatting.Indented));
                defcfgfp.Dispose();

                Console.WriteLine("Default configuration created at location specified.");
                return;
            }

            var cfgfp = File.OpenText(args[1]);

            var cfg = JsonConvert.DeserializeObject<ProgramConfig>(cfgfp.ReadToEnd());

            cfgfp.Dispose();

            var handler = new ServerHandler(
                auth_url: cfg.auth_url,
                db_url: cfg.db_url
            );
        }
    }
}
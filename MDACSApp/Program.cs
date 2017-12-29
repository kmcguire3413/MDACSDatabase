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
        public static async Task<API.Responses.AuthCheckResponse> ReadMessageFromStreamAndAuthenticate(ServerState shandler, int max_size, Stream input_stream)
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
    
    static class Handlers
    {
        public static async Task<Task> Index(ServerState shandler, HTTPRequest request, Stream body, IProxyHTTPEncoder encoder)
        {
            return Task.CompletedTask;
        }

        public static async Task<Task> Viewer(ServerState shandler, HTTPRequest request, Stream body, IProxyHTTPEncoder encoder)
        {
            return Task.CompletedTask;
        }

        public static async Task<Task> Utility(ServerState shandler, HTTPRequest request, Stream body, IProxyHTTPEncoder encoder)
        {
            return Task.CompletedTask;
        }
    }

    internal class ServerState
    {
        public String auth_url;
        public String db_url;

        public ServerState(
            String auth_url,
            String db_url
        )
        {
            this.auth_url = auth_url;
            this.db_url = db_url;
        }
    }

    struct ProgramConfig
    {
        public string auth_url;
        public string db_url;
        public ushort port;
        public string ssl_cert_path;
        public string ssl_cert_pass;
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

            var server_state = new ServerState(
                auth_url: cfg.auth_url,
                db_url: cfg.db_url
            );

            var handlers = new Dictionary<String, SimpleServer<ServerState>.SimpleHTTPHandler>();

            handlers.Add("/", Handlers.Index);

            var server_task = SimpleServer<ServerState>.Create(
                server_state,
                handlers,
                cfg.port,
                cfg.ssl_cert_path, 
                cfg.ssl_cert_pass
            );

            server_task.Wait();
        }
    }
}
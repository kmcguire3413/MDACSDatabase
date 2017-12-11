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

namespace MDACS.Server
{
    /*
        {
          "security_id": "2bad9cc76a04803e19327bf88e2ea8f986d5340e932d97017505c2430855314ef8bc05b9deb6aca15f40832fdb58894ce5a2ddef45c2eb1729e0f15ebfecd416", 
          "node": "2017-12-08_bstewart_7H00665_232030_mp4.0", 
          "duration": 1.9, 
          "metatime": 1512848871.9615905, 
          "fqpath": "/var/mdacs/camerasys_secure/data/2017-12-08_bstewart_7H00665_232030_mp4.0", 
          "userstr": "bstewart", 
          "timestr": "232030", 
          "devicestr": "7H00665", 
          "datestr": "2017-12-08", 
          "datatype": "mp4", 
          "datasize": 21004288, 
          "versions": [
            ["low", "2bad9cc76a04803e19327bf88e2ea8f986d5340e932d97017505c2430855314ef8bc05b9deb6aca15f40832fdb58894ce5a2ddef45c2eb1729e0f15ebfecd416&low"]
          ], 
          "transcoding": false, 
          "note": "Trash", 
        }
    */

    struct Item
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
    }

    class HTTPClient3: HTTPClient2
    {
        private ServerHandler shandler;

        public HTTPClient3(IHTTPServerHandler shandler, HTTPDecoder decoder, HTTPEncoder encoder) : base(shandler, decoder, encoder)
        {
            this.shandler = shandler as ServerHandler;
        }

        class AuthenticationException : Exception
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
                    reply.data[x++] = pair.Value;
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

        public override async Task HandleRequest2(HTTPRequest request, Stream body, ProxyHTTPEncoder encoder)
        {
            var url = request.url_absolute;

            Console.WriteLine("url={0}", url);

            switch (url)
            {
                case "/device-config":
                    break;
                case "/upload":
                    break;
                case "/download":
                    break;
                case "/data":
                    HandleData(request, body, encoder);
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
        }
    }

    class ServerHandler : IHTTPServerHandler
    {
        public Dictionary<String, Item> items;

        public ServerHandler(String metajournal_path, String data_path)
        {
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
                    throw new Exception("A hash failed to match upon loading the metadata journal (metajournal).");
                }

                var metaitem = JsonConvert.DeserializeObject<Item>(meta);

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
                ""
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

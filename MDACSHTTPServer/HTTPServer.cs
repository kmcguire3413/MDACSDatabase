using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace MDACS.Server
{
    public abstract class IHTTPServerHandler
    {
        public abstract HTTPClient CreateClient(IHTTPServerHandler shandler, HTTPDecoder decoder, HTTPEncoder encoder); 
    }

    public class HTTPServer<C> where C: IHTTPServerHandler
    {
        private String pfx_cert_path;
        private String cert_private_key_password;
        private C handler;

        public HTTPServer(C handler, String pfx_cert_path, String cert_private_key_password)
        {
            this.handler = handler;
            this.pfx_cert_path = pfx_cert_path;
            this.cert_private_key_password = cert_private_key_password;
        }

        public async void Start() {
            TcpListener listener = new TcpListener(IPAddress.Any, 8080);

            listener.Start();

            var x509 = new X509Certificate2(pfx_cert_path, cert_private_key_password);

            while (true)
            {
                var client = listener.AcceptTcpClient();
                var ssl_sock = new SslStream(client.GetStream(), false);

#pragma warning disable 4014
                Task.Run(async () =>
                {
                    Console.WriteLine("Authenticating as server through SSL/TLS.");

                    await ssl_sock.AuthenticateAsServerAsync(x509);

                    var http_decoder = new HTTPDecoder(ssl_sock);
                    var http_encoder = new HTTPEncoder(ssl_sock);
                    var http_client = handler.CreateClient(handler, http_decoder, http_encoder);

                    Console.WriteLine("Handling client.");
                    await http_client.Handle();
                });
#pragma warning restore 4014
            }
        }
    }
}

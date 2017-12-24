using MDACS.Server;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using static MDACS.API.Database;
using static MDACS.Server.HTTPClient2;

namespace MDACS.Database
{
    internal static class HandleData
    {
        public class HandleDataReply
        {
            public Item[] data;
        }

        public static async Task Action(ServerHandler shandler, HTTPRequest request, Stream body, ProxyHTTPEncoder encoder)
        {
            var auth_resp = await Helpers.ReadMessageFromStreamAndAuthenticate(shandler, 1024 * 16, body);

            if (!auth_resp.success)
            {
                throw new UnauthorizedException();
            }

            Console.WriteLine("data fetch validated user OK");

            var reply = new HandleDataReply();

            reply.data = new Item[shandler.items.Count];

            lock (shandler.items)
            {
                int x = 0;

                foreach (var pair in shandler.items)
                {
                    // PRIVCHECK MARK
                    if (
                        Helpers.CanUserSeeItem(auth_resp.user, pair.Value)
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
    }
}

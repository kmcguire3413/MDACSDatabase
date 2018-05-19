using MDACS.Server;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using static MDACS.API.Database;
using System.Diagnostics;

namespace MDACS.Database
{
    internal static class HandleData
    {
        public class HandleDataReply
        {
            public Item[] data;
        }

        public static async Task<Task> Action(ServerHandler shandler, HTTPRequest request, Stream body, IProxyHTTPEncoder encoder)
        {
            var auth_resp = await Helpers.ReadMessageFromStreamAndAuthenticate(shandler, 1024 * 16, body);

            if (!auth_resp.success)
            {
                return encoder.Response(403, "Denied").SendNothing();
            }

            var reply = new HandleDataReply();

            reply.data = new Item[shandler.items.Count];

            lock (shandler.items)
            {
                int x = 0;

                foreach (var pair in shandler.items)
                {
                    if (
                        Helpers.CanUserSeeItem(auth_resp.user, pair.Value)
                    )
                    {
                        reply.data[x++] = pair.Value;
                    }
                }
            }

            return encoder.Response(200, "OK").SendJsonFromObject(reply);
        }
    }
}

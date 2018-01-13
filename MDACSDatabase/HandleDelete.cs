using MDACS.API.Requests;
using MDACS.API.Responses;
using MDACS.Database;
using MDACS.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MDACS.Database
{
    static class HandleDelete
    {
        /// <summary>
        /// Handles deleting existing data files. Requires authentication and a special privilege.
        /// </summary>
        /// <param name="shandler"></param>
        /// <param name="request"></param>
        /// <param name="body"></param>
        /// <param name="encoder"></param>
        /// <returns></returns>
        public static async Task<Task> Action(ServerHandler shandler, HTTPRequest request, Stream body, IProxyHTTPEncoder encoder)
        {
            var auth_resp = await Helpers.ReadMessageFromStreamAndAuthenticate(shandler, 1024 * 16, body);

            if (!auth_resp.success)
            {
                throw new UnauthorizedException();
            }

            if (!auth_resp.user.can_delete)
            {
                throw new UnauthorizedException();
            }

            var sreq = JsonConvert.DeserializeObject<DeleteRequest>(auth_resp.payload);

            var sid = sreq.sid;

            if (shandler.items.ContainsKey(sid))
            {
                var item = shandler.items[sid];

                try
                {
                    File.Delete(item.fqpath);
                } catch (Exception _)
                {
                    await encoder.WriteQuickHeaderAndStringBody(
                        500, "Error", JsonConvert.SerializeObject(new DeleteResponse()
                        {
                            success = false,
                        })
                    );
                    return Task.CompletedTask;
                }

                if (item.fqpath != null && item.fqpath.Length > 0)
                {
                    shandler.UsedSpaceSubtract((long)item.datasize);
                }

                item.fqpath = null;

                await shandler.WriteItemToJournal(item);
            }

            await encoder.WriteQuickHeaderAndStringBody(
                200, "Deleted", JsonConvert.SerializeObject(new DeleteResponse()
                {
                    success = true,
                })
            );
            return Task.CompletedTask;
        }
    }
}
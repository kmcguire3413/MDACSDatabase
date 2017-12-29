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
    class SpaceInfoRequest
    {
    }

    static class HandleSpaceInfo
    {
        /// <summary>
        /// Handles returning information about space/bytes usage of the database.
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

            var sreq = JsonConvert.DeserializeObject<DeleteRequest>(auth_resp.payload);

            var resp = new JObject();

            resp["success"] = true;
            resp["used_bytes"] = shandler.GetUsedSpace();
            resp["max_bytes"] = shandler.GetMaxSpace();

            await encoder.WriteQuickHeader(200, "OK");
            await encoder.BodyWriteSingleChunk(JsonConvert.SerializeObject(resp));

            return Task.CompletedTask;
        }
    }
}
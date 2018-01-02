using MDACS.API.Responses;
using MDACS.Database;
using MDACS.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MDACS.Database
{
    static class HandleVersion
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
                await encoder.WriteQuickHeader(403, "Not Allowed");
                await encoder.BodyWriteSingleChunk("");
                return Task.CompletedTask;
            }

            var ver = Assembly.GetExecutingAssembly().GetName().Version;

            var resp = new VersionResponse()
            {
                major = ver.Major,
                minor = ver.Minor,
                build = ver.Build,
                revision = ver.Revision,
            };

            await encoder.WriteQuickHeader(200, "OK");
            await encoder.BodyWriteSingleChunk(JsonConvert.SerializeObject(resp));
            return Task.CompletedTask;
        }
    }
}
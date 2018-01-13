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
    class InternalVersonInfo
    {
        public int major;
        public int minor;
        public int build;
        public int revision;
    }

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
            var buf = new byte[512];
            int cnt;

            do
            {
                cnt = await body.ReadAsync(buf, 0, buf.Length);
            } while (cnt > 0);


            var resp = new VersionResponse()
            {
                major = 0,
                minor = 0,
                build = 0,
                revision = 0,
            };

            InternalVersonInfo ver_info;

            using (var strm = Assembly.GetExecutingAssembly().GetManifestResourceStream("MDACSDatabase.buildinfo.json"))
            {
                var json_data = await new StreamReader(strm).ReadToEndAsync();

                ver_info = JsonConvert.DeserializeObject<InternalVersonInfo>(json_data);
            }

            resp.major = ver_info.major;
            resp.minor = ver_info.minor;
            resp.build = ver_info.build;
            resp.revision = ver_info.revision;

            await encoder.WriteQuickHeader(200, "OK");
            await encoder.BodyWriteSingleChunk(JsonConvert.SerializeObject(resp));
            return Task.CompletedTask;
        }
    }
}
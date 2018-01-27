using MDACS.API.Requests;
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
    static class HandleConfigRequest
    {
        public static async Task<Task> Action(ServerHandler shandler, HTTPRequest request, Stream body, IProxyHTTPEncoder encoder)
        {
            await MDACS.Server.Util.ReadStreamUntilEndAndDiscardDataAsync(body);

            var resp = new JObject();

            resp["dbUrl"] = ".";
            resp["authUrl"] = shandler.auth_url;

            await encoder.WriteQuickHeader(200, "OK");
            await encoder.BodyWriteSingleChunk(JsonConvert.SerializeObject(resp));

            return Task.CompletedTask;
        }
    }
}
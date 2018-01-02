using MDACS.API.Requests;
using MDACS.Server;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace MDACS.Database
{
    internal static class HandleCommitConfiguration
    {
        public static async Task<Task> Action(ServerHandler shandler, HTTPRequest request, Stream body, IProxyHTTPEncoder encoder)
        {
            var auth = await Helpers.ReadMessageFromStreamAndAuthenticate(shandler, 1024 * 16, body);

            if (!auth.success)
            {
                throw new UnauthorizedException();
            }

            if (!auth.user.admin)
            {
                throw new UnauthorizedException();
            }

            var req = JsonConvert.DeserializeObject<CommitConfigurationRequest>(auth.payload);

            var fp = File.OpenWrite(
                Path.Combine(shandler.config_path, String.Format("config_{0}", req.deviceid))
            );

            // TODO: *think* reliable operation and atomic as possible
            var config_bytes_utf8 = Encoding.UTF8.GetBytes(req.config_data);
            await fp.WriteAsync(config_bytes_utf8, 0, config_bytes_utf8.Length);
            fp.Dispose();

            await encoder.WriteQuickHeader(200, "OK");
            await encoder.BodyWriteSingleChunk("{ \"success\": true }");

            return Task.CompletedTask;
        }
    }
}

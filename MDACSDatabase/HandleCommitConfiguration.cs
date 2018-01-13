using MDACS.API.Requests;
using MDACS.API.Responses;
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
        class ConfigFileData
        {
            public string userid;
            public string config_data;
        }

        public static async Task<Task> Action(ServerHandler shandler, HTTPRequest request, Stream body, IProxyHTTPEncoder encoder)
        {
            var auth = await Helpers.ReadMessageFromStreamAndAuthenticate(shandler, 1024 * 16, body);

            if (!auth.success)
            {
                await encoder.WriteQuickHeader(403, "Must be authenticated.");
                await encoder.BodyWriteSingleChunk(JsonConvert.SerializeObject(new CommitConfigurationResponse()
                {
                    success = false,
                }));
                return Task.CompletedTask;
            }

            if (!auth.user.admin)
            {
                await encoder.WriteQuickHeader(403, "Must be admin.");
                await encoder.BodyWriteSingleChunk(JsonConvert.SerializeObject(new CommitConfigurationResponse()
                {
                    success = false,
                }));
                return Task.CompletedTask;
            }

            var req = JsonConvert.DeserializeObject<CommitConfigurationRequest>(auth.payload);

            var fp = File.OpenWrite(
                Path.Combine(shandler.config_path, $"config_{req.deviceid}.data")
            );

            var file_data = new ConfigFileData()
            {
                userid = req.userid,
                config_data = req.config_data,
            };

            // TODO: *think* reliable operation and atomic as possible
            var config_bytes_utf8 = Encoding.UTF8.GetBytes(
                JsonConvert.SerializeObject(file_data)
            );
            await fp.WriteAsync(config_bytes_utf8, 0, config_bytes_utf8.Length);
            fp.Dispose();

            await encoder.WriteQuickHeader(200, "OK");
            await encoder.BodyWriteSingleChunk(JsonConvert.SerializeObject(new CommitConfigurationResponse()
            {
                success = true,
            }));
            return Task.CompletedTask;
        }
    }
}

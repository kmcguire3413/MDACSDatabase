using MDACS.API.Requests;
using MDACS.API.Responses;
using MDACS.Server;
using MDACSAPI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace MDACS.Database
{
    static class HandleDeviceConfig
    {
        public static async Task<Task> Action(ServerHandler shandler, HTTPRequest request, Stream body, IProxyHTTPEncoder encoder)
        {
            var buf = new byte[4096];
            int ndx = 0;
            int cnt;

            while ((cnt = await body.ReadAsync(buf, ndx, buf.Length - ndx)) > 0) {
                ndx += cnt;
            }

            var buf_utf8 = Encoding.UTF8.GetString(buf, 0, ndx);

            Logger.WriteDebugString($"buf_utf8={buf_utf8}");

            var req = JsonConvert.DeserializeObject<DeviceConfigRequest>(buf_utf8);

            var path = Path.Combine(shandler.config_path, $"config_{req.deviceid}.data");

            if (!File.Exists(path))
            {
                var _fp = File.OpenWrite(
                    path
                );

                var _tmp = new JObject();

                _tmp["userid"] = null;
                _tmp["config_data"] = req.current_config_data;

                var _config_bytes_utf8 = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_tmp));
                await _fp.WriteAsync(_config_bytes_utf8, 0, _config_bytes_utf8.Length);
                _fp.Dispose();
            }

            Logger.WriteDebugString($"The config path is {path}.");

            var fp = File.OpenRead(
                path
            );

            var config_bytes_utf8 = new byte[fp.Length];

            await fp.ReadAsync(config_bytes_utf8, 0, config_bytes_utf8.Length);

            fp.Dispose();
                
            var config_data = Encoding.UTF8.GetString(config_bytes_utf8);

            JObject tmp = JsonConvert.DeserializeObject<JObject>(config_data);

            tmp["config_data"] = JsonConvert.SerializeObject(
                JsonConvert.DeserializeObject<JObject>(tmp["config_data"].Value<string>()),
                Formatting.Indented
            );

            tmp["config_data"] = tmp["config_data"].Value<string>().Replace("\n", "\r\n");

            var resp = new DeviceConfigResponse();

            resp.success = true;
            resp.config_data = JsonConvert.SerializeObject(tmp);

            Debug.WriteLine($"@@@@@ {resp.config_data}");

            await encoder.WriteQuickHeader(200, "OK");
            await encoder.BodyWriteSingleChunk(JsonConvert.SerializeObject(resp));
            return Task.CompletedTask;
        }
    }
}

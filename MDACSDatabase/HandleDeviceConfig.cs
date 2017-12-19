using MDACS.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using static MDACS.Server.HTTPClient2;

namespace MDACS.Database
{
    static class HandleDeviceConfig
    {
        class HandleDeviceConfigRequest
        {
            public String deviceid;
            public String current_config_data;
        }

        struct HandleDeviceConfigResponse
        {
            public bool success;
            public String config_data;
        }

        public static async Task Action(ServerHandler shandler, HTTPRequest request, Stream body, ProxyHTTPEncoder encoder)
        {
            var buf = new byte[4096];
            int ndx = 0;
            int cnt;

            while ((cnt = body.Read(buf, 0, buf.Length - ndx)) > 0) {
                ndx += cnt;
            }

            var buf_utf8 = Encoding.UTF8.GetString(buf, 0, ndx);

            var req = JsonConvert.DeserializeObject<HandleDeviceConfigRequest>(buf_utf8);

            var path = Path.Combine(shandler.config_path, String.Format("config_{0}.data", req.deviceid));

            if (!File.Exists(path))
            {
                var _fp = File.OpenWrite(
                    path
                );

                var _tmp = new JObject();

                _tmp["config_data"] = req.current_config_data;

                var _config_bytes_utf8 = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_tmp));
                await _fp.WriteAsync(_config_bytes_utf8, 0, _config_bytes_utf8.Length);
                _fp.Dispose();
            }
            var fp = File.OpenRead(
                path
            );

            fp.Seek(0, SeekOrigin.End);

            var config_bytes_utf8 = new byte[fp.Position];

            fp.Seek(0, SeekOrigin.Begin);

            await fp.ReadAsync(config_bytes_utf8, 0, config_bytes_utf8.Length);
            fp.Dispose();

            HandleDeviceConfigResponse resp;

            resp.success = true;
                
            var config_data = Encoding.UTF8.GetString(config_bytes_utf8);

            JObject tmp = JsonConvert.DeserializeObject<JObject>(config_data);

            tmp["config_data"] = JsonConvert.SerializeObject(
                JsonConvert.DeserializeObject<JObject>(tmp["config_data"].Value<String>()),
                Formatting.Indented
            );

            tmp["config_data"] = tmp["config_data"].Value<String>().Replace("\n", "\r\n");

            resp.config_data = JsonConvert.SerializeObject(tmp);

            await encoder.WriteQuickHeader(200, "OK");
            await encoder.BodyWriteSingleChunk(JsonConvert.SerializeObject(resp));
        }
    }
}

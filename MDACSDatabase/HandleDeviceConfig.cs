using MDACS.Server;
using Newtonsoft.Json;
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
            public String config_data;
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

            var path = Path.Combine(shandler.config_path, String.Format("config_{0}", req.deviceid));

            if (!File.Exists(path))
            {
                var fp = File.OpenWrite(
                    path
                );

                var config_bytes_utf8 = Encoding.UTF8.GetBytes(req.config_data);
                await fp.WriteAsync(config_bytes_utf8, 0, config_bytes_utf8.Length);
                fp.Dispose();

                HandleDeviceConfigResponse resp;

                resp.success = true;
                resp.config_data = req.config_data;

                await encoder.WriteQuickHeader(200, "OK");
                await encoder.BodyWriteSingleChunk(JsonConvert.SerializeObject(resp));
            }
            else
            {
                var fp = File.OpenRead(
                    path
                );

                var config_bytes_utf8 = new byte[fp.Length];
                await fp.ReadAsync(config_bytes_utf8, 0, config_bytes_utf8.Length);
                fp.Dispose();

                HandleDeviceConfigResponse resp;

                resp.success = true;
                resp.config_data = Encoding.UTF8.GetString(config_bytes_utf8);

                await encoder.WriteQuickHeader(200, "OK");
                await encoder.BodyWriteSingleChunk(JsonConvert.SerializeObject(resp));
            }
        }
    }
}

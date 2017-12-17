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
    internal static class HandleEnumerateConfigurations
    {
        public class HandleEnumerateConfigurationsResponse
        {
            public bool success;
            public Dictionary<String, String> configs;
        }

        public static async Task Action(ServerHandler shandler, HTTPRequest request, Stream body, ProxyHTTPEncoder encoder)
        {
            var auth = await Helpers.ReadMessageFromStreamAndAuthenticate(shandler, 1024 * 16, body);

            if (!auth.success)
            {
                throw new UnauthorizedException();
            }

            var resp = new HandleEnumerateConfigurationsResponse();

            resp.configs = new Dictionary<string, string>();

            try
            {
                foreach (var node in Directory.EnumerateFiles(shandler.config_path))
                {
                    var fnode = Path.Combine(shandler.data_path, node);

                    if (fnode.StartsWith("config_"))
                    {
                        var fd = File.OpenRead(fnode);

                        var buf = new byte[fd.Length];

                        int cnt = 0;

                        while (cnt < buf.Length)
                        {
                            cnt += await fd.ReadAsync(buf, cnt, buf.Length - cnt);
                        }

                        var buf_text = Encoding.UTF8.GetString(buf);

                        resp.configs[fnode.Substring(fnode.IndexOf("_") + 1)] = buf_text;

                        fd.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new ProgramException("Error happened during enumeration of configurations.", ex);
            }

            using (var de_stream = new DoubleEndedStream())
            {
                await encoder.WriteQuickHeader(200, "OK");
                var tmp = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(resp));
                await encoder.BodyWriteStream(de_stream);
                await de_stream.WriteAsync(tmp, 0, tmp.Length);
            }
        }
    }
}

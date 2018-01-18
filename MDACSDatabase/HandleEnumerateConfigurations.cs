using MDACS.API.Responses;
using MDACS.Server;
using MDACSAPI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace MDACS.Database
{
    internal static class HandleEnumerateConfigurations
    {
        public static async Task<Task> Action(ServerHandler shandler, HTTPRequest request, Stream body, IProxyHTTPEncoder encoder)
        {
            var auth = await Helpers.ReadMessageFromStreamAndAuthenticate(shandler, 1024 * 16, body);

            if (!auth.success)
            {
                return encoder.Response(403, "Denied").SendNothing();
            }

            var resp = new EnumerateConfigurationsResponse();

            resp.success = true;

            resp.configs = new Dictionary<string, string>();

            try
            {
                foreach (var node in Directory.EnumerateFiles(shandler.config_path))
                {
                    var fnode = Path.Combine(shandler.data_path, node);

                    var fnode_filename = Path.GetFileName(fnode);

                    if (fnode_filename.StartsWith("config_") && fnode_filename.EndsWith(".data"))
                    {
                        var fd = File.OpenRead(fnode);

                        var buf = new byte[fd.Length];

                        int cnt = 0;

                        while (cnt < buf.Length)
                        {
                            cnt += await fd.ReadAsync(buf, cnt, buf.Length - cnt);
                        }

                        var buf_text = Encoding.UTF8.GetString(buf);

                        var id = fnode_filename.Substring(fnode_filename.IndexOf("_") + 1);

                        id = id.Substring(0, id.LastIndexOf("."));

                        resp.configs[id] = buf_text;

                        fd.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.WriteCriticalString($"Error during configuration enumeration as follows:\n{ex}");

                return encoder.Response(500, "Error").SendNothing();
            }

            return encoder.Response(200, "OK").SendJsonFromObject(resp);
        }
    }
}

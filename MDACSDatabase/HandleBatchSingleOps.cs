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
    internal static class HandleBatchSingleOps
    {
        public class HandleBatchSingleOpsResponse
        {
            public bool success;
            public String[][] failed;
        }

        public class HandleBatchSingleOpsRequest
        {
            public String[][] ops;
        }

        public static async Task Action(ServerHandler shandler, HTTPRequest request, Stream body, ProxyHTTPEncoder encoder)
        {
            var auth = await Helpers.ReadMessageFromStreamAndAuthenticate(shandler, 1024 * 16, body);

            Console.WriteLine(String.Format("auth.success={0}", auth.success));

            if (!auth.success)
            {
                throw new UnauthorizedException();
            }

            Console.WriteLine(String.Format("auth.payload={0}", auth.payload));

            var req = JsonConvert.DeserializeObject<HandleBatchSingleOpsRequest>(auth.payload);

            List<String[]> failed = new List<String[]>();

            var tasks = new List<Task<bool>>();

            foreach (var op in req.ops)
            {
                if (op.Length < 3)
                {
                    continue;
                }

                var sid = op[0];
                var key = op[1];
                var val = op[2];

                lock (shandler.items)
                {
                    if (shandler.items.ContainsKey(sid))
                    {
                        try
                        {
                            // Must pull locally, make change, then push into items. Else, we only modify our
                            // frame local value. This is because Item is a struct/value type and not a reference
                            // type in order to keep memory more compact.
                            var tmp = shandler.items[sid];
                            tmp.GetType().GetField(key).SetValue(shandler.items[sid], val);
                            shandler.items[sid] = tmp;

                            tasks.Add(shandler.WriteItemToJournal(tmp));
                        }
                        catch (Exception ex)
                        {
                            failed.Add(new String[] { sid, key, val, ex.ToString() });
                        }
                    }
                }
            }

            Task.WaitAll(tasks.ToArray());

            using (var de_stream = new DoubleEndedStream())
            {
                var resp = new HandleBatchSingleOpsResponse();

                resp.success = true;
                resp.failed = failed.ToArray();

                await encoder.WriteQuickHeader(200, "OK");
                var tmp = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(resp));
                await encoder.BodyWriteStream(de_stream);
                await de_stream.WriteAsync(tmp, 0, tmp.Length);
            }
        }
    }
}

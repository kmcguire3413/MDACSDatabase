using MDACS.Server;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

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

        public static async Task<Task> Action(ServerHandler shandler, HTTPRequest request, Stream body, IProxyHTTPEncoder encoder)
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
                            // TODO: make the type variant so the caller can specifiy things other 
                            //       than string; will need to interpret the entire structure as a
                            //       JSON object to do it easily
                            var tmp = shandler.items[sid];
                            var field = tmp.GetType().GetField(key);
                            field.SetValue(shandler.items[sid], val);
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

            return Task.CompletedTask;
        }
    }
}

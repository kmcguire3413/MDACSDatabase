using MDACS.API.Requests;
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
    internal static class HandleBatchSingleOps
    {
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

            var failed = new List<BatchSingleOp>();

            var tasks = new List<Task<bool>>();

            foreach (var op in req.ops)
            {
                var sid = op.sid;
                var field_name = op.field_name;
                var value = op.value;

                lock (shandler.items)
                {
                    if (shandler.items.ContainsKey(sid))
                    {
                        try
                        {
                            var tmp = shandler.items[sid];
                            var field = tmp.GetType().GetField(field_name);
                            field.SetValue(tmp, value.ToObject(field.FieldType));
                            tasks.Add(shandler.WriteItemToJournal(tmp));
                        }
                        catch (Exception ex)
                        {
#if DEBUG
                            Logger.WriteDebugString($"Failed during batch single operation. The SID was {sid}. The field name was {field_name}. The value was {value}. The error was:\n{ex}");
#endif
                            failed.Add(new BatchSingleOp()
                            {
                                field_name = field_name,
                                sid = sid,
                                value = value,
                            });
                        }
                    } else
                    {
                        failed.Add(new BatchSingleOp()
                        {
                            field_name = field_name,
                            sid = sid,
                            value = value,
                        });
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

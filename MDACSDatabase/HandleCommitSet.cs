using MDACS.Server;
using MDACSAPI;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using static MDACS.API.Database;

namespace MDACS.Database
{
    internal static class HandleCommitSet
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="request"></param>
        /// <param name="body"></param>
        /// <param name="encoder"></param>
        /// <exception cref="UnauthorizedException">User has insufficient priviledges to modify the item.</exception>
        /// <exception cref="AuthenticationException">User is not valid for access of any type.</exception>
        /// <exception cref="InvalidArgumentException">One of the arguments was not correct or the reason for failure.</exception>
        /// <exception cref="ProgramException">Anything properly handled but needs handling for acknowlegement purposes.</exception>
        /// <exception cref="Exception">Anything else could result in instability.</exception>
        public static async Task<Task> Action(ServerHandler shandler, HTTPRequest request, Stream body, IProxyHTTPEncoder encoder)
        {
            var auth_resp = await Helpers.ReadMessageFromStreamAndAuthenticate(shandler, 1024 * 16, body);

            if (!auth_resp.success)
            {
                throw new UnauthorizedException();
            }

            var sreq = JsonConvert.DeserializeObject<API.Requests.CommitSetRequest>(auth_resp.payload);

            Monitor.Enter(shandler.items);

            Item item;

#if DEBUG
            Logger.WriteDebugString($"sreq.security_id={sreq.security_id}");
#endif

            try
            {
                if (!shandler.items.ContainsKey(sreq.security_id))
                {
                    await encoder.WriteQuickHeader(404, "Not Found");
                    await encoder.BodyWriteSingleChunk("");
                    return Task.CompletedTask;
                }

                item = shandler.items[sreq.security_id];
            }
            catch (Exception ex)
            {
#if DEBUG
                Logger.WriteDebugString($"Exception on getting item was:\n{ex}");
#endif
                await encoder.WriteQuickHeader(500, "Error");
                await encoder.BodyWriteSingleChunk("");
                return Task.CompletedTask;
            }
            finally
            {
                Monitor.Exit(shandler.items);
            }

            if (!Helpers.CanUserModifyItem(auth_resp.user, item))
            {
#if DEBUG
                Logger.WriteDebugString($"User was not authorized to write to item.");
#endif
                await encoder.WriteQuickHeader(403, "Not Authorized");
                await encoder.BodyWriteSingleChunk("");
                return Task.CompletedTask;
            }

            try
            {
                foreach (var pair in sreq.meta)
                {
                    // Reflection simplified coding time at the expense of performance.
                    var field = item.GetType().GetField(pair.Key);
                    field.SetValue(item, pair.Value.ToObject(field.FieldType));
#if DEBUG
                    Logger.WriteDebugString($"Set field {field} of {sreq.meta} to {pair.Value.ToString()}.");
#endif                    
                }

                shandler.items[sreq.security_id] = item;

                if (!await shandler.WriteItemToJournal(item))
                {
#if DEBUG
                    Logger.WriteDebugString($"Error happened when writing to the journal for a commit set operation.");
#endif
                    await encoder.WriteQuickHeader(500, "Error");
                    await encoder.BodyWriteSingleChunk("");
                    return Task.CompletedTask;
                }
            }
            catch (Exception)
            {
#if DEBUG
                Logger.WriteDebugString($"Error happened when writing to journal or setting item fields during commit set operation.");
#endif
                await encoder.WriteQuickHeader(500, "Error");
                await encoder.BodyWriteSingleChunk("");
                return Task.CompletedTask;
            }

            var resp = new MDACS.API.Responses.CommitSetResponse();

            resp.success = true;

            await encoder.WriteQuickHeader(200, "OK");
            await encoder.BodyWriteSingleChunk(JsonConvert.SerializeObject(resp));
            return Task.CompletedTask;
        }
    }
}

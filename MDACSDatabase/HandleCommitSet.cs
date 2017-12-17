using MDACS.Server;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static MDACS.Server.HTTPClient2;

namespace MDACS.Database
{
    internal static class HandleCommitSet
    {
        public class HandleCommitSetRequest
        {
            public String security_id;
            public Dictionary<String, String> meta;
        }

        public class HandleCommitSetResponse
        {
            public bool success;
        }

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
        public static async Task Action(ServerHandler shandler, HTTPRequest request, Stream body, ProxyHTTPEncoder encoder)
        {
            var auth_resp = await Helpers.ReadMessageFromStreamAndAuthenticate(shandler, 1024 * 16, body);

            if (!auth_resp.success)
            {
                throw new UnauthorizedException();
            }

            var sreq = JsonConvert.DeserializeObject<HandleCommitSetRequest>(auth_resp.payload);

            Monitor.Enter(shandler.items);

            Item item;

            try
            {
                if (!shandler.items.ContainsKey(sreq.security_id))
                {
                    throw new InvalidArgumentException();
                }

                item = shandler.items[sreq.security_id];
            }
            catch (Exception ex)
            {
                throw new ProgramException("Unidentified problem.", ex);
            }
            finally
            {
                Monitor.Exit(shandler.items);
            }

            if (!Helpers.CanUserModifyItem(auth_resp.user, item))
            {
                throw new UnauthorizedException();
            }

            try
            {
                foreach (var pair in sreq.meta)
                {
                    // Reflection simplified coding time at the expense of performance.
                    item.GetType().GetProperty(pair.Key).SetValue(item, pair.Value);
                    Logger.LogLine(String.Format("Set property {0} for item to {1}.", pair.Key, pair.Value));
                }

                shandler.items[sreq.security_id] = item;

                if (!await shandler.WriteItemToJournal(item))
                {
                    throw new ProgramException("Unable to write to the journal.");
                }
            }
            catch (Exception ex)
            {
                throw new ProgramException("Caught inner exception when setting item with commit set command.", ex);
            }

            // Success.
            await encoder.WriteQuickHeader(200, "OK");
            await encoder.BodyWriteSingleChunk("{ \"success\": true }");
        }
    }
}

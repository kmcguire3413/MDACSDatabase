using MDACS.Server;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using static MDACS.API.Database;

namespace MDACS.Database
{
    internal static class HandleData
    {
        public class HandleDataReply
        {
            public Item[] data;
        }

        private static string PerformDeveloperPrivacyOnString(string s) {
            var sb = new StringBuilder();

            char delta = (char)(DateTime.Now.Day + 3);

            foreach (var c in s) {
                char _base;
                char _cap;

                if (c >= 'a' && c <= 'z') {
                    _base = 'a';
                    _cap = (char)('z' - 'a');
                } else if (c >= 'A' && c <= 'Z') {
                    _base = 'A';
                    _cap = (char)('Z' - 'A');
                } else if (c >= '0' && c <= '9') {
                    _base = '0';
                    _cap = (char)('9' - '0');
                } else {
                    sb.Append(c);
                    continue;
                }

                char nc = (char)(((c - _base) + delta) % _cap + _base);

                sb.Append(nc);
            }

            return sb.ToString();
        }

        private static Item PerformDeveloperPrivacyOnItem(Item item) {
            var tmp = new Item();

            tmp.datestr = item.datestr;
            tmp.timestr = item.timestr;

            tmp.datasize = item.datasize;
            tmp.datatype = item.datatype;
            tmp.data_hash_sha512 = item.data_hash_sha512;
            tmp.duration = item.duration;
            tmp.fqpath = item.fqpath;
            tmp.manager_uuid = item.manager_uuid;
            tmp.metatime = item.metatime;
            tmp.node = item.node;
            tmp.security_id = item.security_id;
            tmp.state = item.state;

            tmp.devicestr = PerformDeveloperPrivacyOnString(item.devicestr);
            tmp.userstr = PerformDeveloperPrivacyOnString(item.userstr);
            tmp.note = PerformDeveloperPrivacyOnString(item.note);

            return tmp;
        }

        public static async Task<Task> Action(ServerHandler shandler, HTTPRequest request, Stream body, IProxyHTTPEncoder encoder)
        {
            var auth_resp = await Helpers.ReadMessageFromStreamAndAuthenticate(shandler, 1024 * 16, body);

            if (!auth_resp.success)
            {
                return encoder.Response(403, "Denied").SendNothing();
            }

            var reply = new HandleDataReply();

            reply.data = new Item[shandler.items.Count];

            lock (shandler.items)
            {
                int x = 0;

                foreach (var pair in shandler.items)
                {
                    // PRIVCHECK MARK
                    if (
                        Helpers.CanUserSeeItem(auth_resp.user, pair.Value)
                    )
                    {
                        if (!auth_resp.user.developer_privacy_feature) {
                            reply.data[x++] = pair.Value;
                        } else {
                            // Allows developer access to live data without comprimise of the
                            // actual data. The data is obfuscated based on the current date.
                            reply.data[x++] = PerformDeveloperPrivacyOnItem(pair.Value);
                        }
                    }
                }
            }

            return encoder.Response(200, "OK").SendJsonFromObject(reply);
        }
    }
}

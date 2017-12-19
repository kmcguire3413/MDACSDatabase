using MDACS.Database;
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
    static class HandleUpload
    {
        private static async Task<bool> WaitForFileSizeMatch(String path, ulong size, int minutes)
        {
            var st = DateTime.Now;
            ulong fsize;

            do
            {
                var fpc = File.OpenRead(path);
                fsize = (ulong)fpc.Length;
                fpc.Dispose();
                await Task.Yield();
            } while (
                fsize < size &&
                (DateTime.Now - st).TotalMinutes < minutes
            );

            if (fsize < size)
            {
                return false;
            }

            return true;
        }

        public static async Task Action(ServerHandler shandler, HTTPRequest request, Stream body, ProxyHTTPEncoder encoder)
        {
            var buf = new byte[4096];
            int bufndx = 0;
            int cnt;
            int tndx;

            do
            {
                cnt = await body.ReadAsync(buf, bufndx, buf.Length - bufndx);

                if (cnt > 0)
                {
                    bufndx += cnt;
                }

                if (bufndx >= buf.Length)
                {
                    throw new ProgramException("On receiving upload header. The header size exceeded 4096-bytes.");
                }

                tndx = Array.IndexOf(buf, (byte)'\r');
            } while (cnt > 0 && tndx < 0);

            var hdrstr = Encoding.UTF8.GetString(buf, 0, tndx).Trim();

            JObject auth_package = JsonConvert.DeserializeObject<JObject>(hdrstr);

            var payload = auth_package["payload"].Value<String>();

            var hdr = JsonConvert.DeserializeObject<MDACS.API.Database.UploadHeader>(payload);

            // 10
            // 20
            // 20 - (10 + 1) = 9

            Array.Copy(buf, tndx + 1, buf, 0, bufndx - (tndx + 1));
            // Quasi-repurpose the variable `bufndx` to mark end of the slack data.
            bufndx = bufndx - (tndx + 1);

            //
            var data_node = String.Format("{0}_{1}_{2}_{3}.{4}",
                hdr.datestr,
                hdr.userstr,
                hdr.devicestr,
                hdr.timestr,
                hdr.datatype
            );

            var data_node_path = Path.Combine(shandler.data_path, data_node);

            var temp_data_node_path = Path.Combine(
                shandler.data_path,
                DateTime.Now.ToFileTime().ToString()
            );

            try
            {
                var fp = File.OpenWrite(temp_data_node_path);
                await fp.WriteAsync(buf, 0, bufndx);
                await body.CopyToAsync(fp)
                await fp.FlushAsync();
                fp.Dispose();
            }
            catch (Exception ex)
            {
                throw new ProgramException("Problem during write to file from body stream.", ex);
            }

            if (!await WaitForFileSizeMatch(temp_data_node_path, hdr.datasize, 3))
            {
                throw new ProgramException("The upload byte length of the destination never reached the intended stream size.");
            }

            try
            {
                File.Move(temp_data_node_path, data_node_path);
            }
            catch (Exception ex)
            {
                throw new ProgramException("Problem when executing move from temp file to actual destination.", ex);
            }

            if (!await WaitForFileSizeMatch(data_node_path, hdr.datasize, 3))
            {
                throw new ProgramException("The upload byte length of the destination never reached the intended stream size, after moving from the temp file.");
            }

            Item item = new Item();

            item.datasize = hdr.datasize;
            item.datatype = hdr.datatype;
            item.datestr = hdr.datestr;
            item.devicestr = hdr.devicestr;
            item.duration = -2.48;
            item.fqpath = data_node_path;
            item.metatime = DateTime.Now.ToFileTimeUtc();
            item.node = data_node;
            item.note = "";
            item.security_id = "";
            item.timestr = hdr.timestr;
            item.userstr = hdr.userstr;
            item.versions = null;
            item.state = "";

            await shandler.WriteItemToJournal(item);

            await encoder.WriteQuickHeader(200, "OK");
            await encoder.BodyWriteSingleChunk("{ \"success\": true }");
        }
    }
}

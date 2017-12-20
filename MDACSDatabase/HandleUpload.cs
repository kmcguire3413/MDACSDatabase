using MDACS.Database;
using MDACS.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using static MDACS.Server.HTTPClient2;

namespace MDACS.Database
{
    static class HandleUpload
    {
        private static async Task<bool> CheckedFileMoveAsync(String from, String to)
        {
            try
            {
                long size;

                using (var fp = File.OpenRead(from))
                {
                    size = fp.Length;
                }

                // If this is synchronous and blocks, then maybe this will throw it into its own thread.
                await Task.Run(() => File.Move(from, to));

                // Once the above completed then watch the file until the size matches the expected.
                await WaitForFileSizeMatch(to, size, 5);
            } catch (Exception _)
            {
                return false;
            }

            return true;
        }

        private static async Task<bool> WaitForFileSizeMatch(String path, long size, int minutes)
        {
            var st = DateTime.Now;
            long fsize;

            do
            {
                var fpc = File.OpenRead(path);
                fsize = fpc.Length;
                fpc.Dispose();
                await Task.Delay(500);

                if (fsize > size)
                {
                    return false;
                }
            } while (
                fsize != size &&
                (DateTime.Now - st).TotalMinutes < minutes
            );

            if (fsize != size)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Handles reading header and data for data upload. A critical routine that employs as many checks as needed
        /// to ensure that written data is verified as written and correct.
        /// </summary>
        /// <param name="shandler"></param>
        /// <param name="request"></param>
        /// <param name="body"></param>
        /// <param name="encoder"></param>
        /// <returns></returns>
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

                tndx = Array.IndexOf(buf, (byte)'\n');

                if (bufndx >= buf.Length && tndx < 0)
                {
                    throw new ProgramException("On receiving upload header. The header size exceeded 4096-bytes.");
                }
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

            // Make the name unique and keep pertinent information in the event something fails.
            var temp_data_node_path = Path.Combine(
                shandler.data_path,
                $"temp_{DateTime.Now.ToFileTime().ToString()}_{data_node}"
            );

            try
            {
                var fp = File.OpenWrite(temp_data_node_path);
                await fp.WriteAsync(buf, 0, bufndx);
                await body.CopyToAsync(fp);
                await fp.FlushAsync();
                fp.Dispose();
            }
            catch (Exception ex)
            {
                File.Delete(temp_data_node_path);
                throw new ProgramException("Problem during write to file from body stream.", ex);
            } 

            if (!await WaitForFileSizeMatch(temp_data_node_path, (long)hdr.datasize, 3))
            {
                File.Delete(temp_data_node_path);
                throw new ProgramException("The upload byte length of the destination never reached the intended stream size.");
            } 

            try
            {
                if (File.Exists(data_node_path))
                {
                    await CheckedFileMoveAsync(data_node_path, $"{data_node_path}.moved.{DateTime.Now.ToFileTime().ToString()}");
                }
                
                await CheckedFileMoveAsync(temp_data_node_path, data_node_path);
            }
            catch (Exception ex)
            {
                // Delete the temporary since we should have saved the original.
                File.Delete(temp_data_node_path);
                // Move the original back to the original filename.
                await CheckedFileMoveAsync($"{data_node_path}.moved.{DateTime.Now.ToFileTime().ToString()}", data_node_path);
                throw new ProgramException("Problem when executing move from temp file to actual destination.", ex);
            }

            if (!await WaitForFileSizeMatch(data_node_path, (long)hdr.datasize, 3))
            {
                throw new ProgramException("The upload byte length of the destination never reached the intended stream size, after moving from the temp file.");
            }

            Item item = new Item();

            var hasher = new SHA512Managed();

            var security_id_bytes = hasher.ComputeHash(Encoding.UTF8.GetBytes(data_node));

            item.datasize = hdr.datasize;
            item.datatype = hdr.datatype;
            item.datestr = hdr.datestr;
            item.devicestr = hdr.devicestr;
            item.duration = -1.0;
            item.fqpath = data_node_path;
            item.metatime = DateTime.Now.ToFileTimeUtc();
            item.node = data_node;
            item.note = "";
            item.security_id = BitConverter.ToString(security_id_bytes).Replace("-", "").ToLower();
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

#define DOUBLE_ENDED_STREAM_DEBUG

using System;
using System.Threading.Tasks;
using System.Text;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Security.Cryptography;
using static MDACS.API.Database;
using Newtonsoft.Json.Linq;
using MDACSAPI;
using System.Net;
using static MDACS.API.Auth;

namespace MDACS.Database
{
    internal class ServerHandler
    {
        public Dictionary<String, Item> items;
        public string auth_url { get;  }
        public string metajournal_path { get; }
        public string data_path { get; }
        public string config_path { get; }
        public string manager_uuid { get; }
        private RSA private_signature_key;

        private long used_space;
        private long max_storage_space;

        /// <summary>
        /// This URL represents the service to handle item upload notifications. On
        /// successful item upload a notification will be posted to this URL.
        /// </summary>
        private string notification_post_url;

        public ServerHandler(
            string metajournal_path,
            string data_path,
            string config_path,
            string auth_url,
            string universal_records_key_path,
            string universal_records_key_pass,
            string universal_records_url,
            int cluster_size,
            long max_storage_space,
            string notification_post_url)
        {
            this.notification_post_url = notification_post_url;
            this.data_path = data_path;
            this.config_path = config_path;
            this.auth_url = auth_url;
            this.metajournal_path = metajournal_path;
            this.used_space = 0;
            this.max_storage_space = max_storage_space;

            this.journal_semaphore = new SemaphoreSlim(1, 1);

            items = new Dictionary<string, Item>();

            if (!File.Exists(metajournal_path))
            {
                File.Create(metajournal_path).Dispose();
            }

            var mj = File.OpenText(metajournal_path);

            var hasher = MD5.Create();

            Console.WriteLine("Reading journal into memory.");

            long line_no = 0;

            while (!mj.EndOfStream)
            {
                Item metaitem;

                line_no++;

                var line = mj.ReadLine();

                var colon_ndx = line.IndexOf(':');

                if (colon_ndx < 0)
                {
                    continue;
                }

                try
                {
                    var hash = line.Substring(0, colon_ndx);
                    var meta = line.Substring(colon_ndx + 1).TrimEnd();

                    var correct_hash = BitConverter.ToString(
                        hasher.ComputeHash(Encoding.UTF8.GetBytes(meta))
                    ).Replace("-", "").ToLower();

                    if (hash != correct_hash)
                    {
                        //mj.Dispose();
                        continue;
                    }

                    metaitem = Item.Deserialize(meta);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    continue;
                }

                var sid = metaitem.security_id;

                if (sid.Length != (512 / 8 * 2))
                {
                    // Throw out entries with a bad sid.
                    continue;
                }

                if (metaitem.fqpath != null && metaitem.fqpath.Length > 0 && metaitem.duration < 1.0)
                {
                    if (File.Exists(metaitem.fqpath))
                    {
                        var dur = MDACS.Database.MediaTools.MP4Info.GetDuration(metaitem.fqpath);

                        Console.WriteLine($"{metaitem.fqpath} {dur}");

                        metaitem.duration = dur;
                    }
                }

                if (items.ContainsKey(metaitem.security_id))
                {
                    // Overwrite existing entries.
                    items[metaitem.security_id] = metaitem;
                }
                else
                {
                    // Add new entries.
                    items.Add(metaitem.security_id, metaitem);
                }
            }

            foreach (var item in items)
            {
                // TODO: Add runtime overflow check. But, how likely is it to have a datasize > 2 ** 63?
                // TODO: Unless using the automatic overflow checks will catch this problem?
                if (item.Value.fqpath != null && item.Value.fqpath.Length > 0)
                {
                    if (File.Exists(item.Value.fqpath))
                    {
                        this.used_space += (long)item.Value.datasize;
                    }
                }
            }

            if (universal_records_key_path != null)
            {
                var x509 = new X509Certificate2(universal_records_key_path, universal_records_key_pass);

                this.private_signature_key = (RSA)x509.PrivateKey;

                if (manager_uuid == null)
                {
                    manager_uuid = new System.Guid().ToString();

                    var entry = new JObject();

                    entry["directive"] = "uuid";
                    entry["uuid"] = manager_uuid;
                    entry["public_key"] = Convert.ToBase64String(x509.PublicKey.EncodedKeyValue.RawData);

                    var tsk = WriteItemToJournal(entry);

                    tsk.Wait();

                    if (tsk.Exception != null)
                    {
                        throw tsk.Exception;
                    }
                }
            }
            Console.WriteLine("Done reading journal into memory.");

            mj.Dispose();
        }

        public String EncryptString(string data)
        {
            var aes = Aes.Create();
            var aes_key_encrypted = private_signature_key.Encrypt(aes.Key, RSAEncryptionPadding.OaepSHA512);
            var enc = aes.CreateEncryptor();
            var data_bytes = Encoding.UTF8.GetBytes(data);
            var data_aes_crypted = enc.TransformFinalBlock(data_bytes, 0, data_bytes.Length);
            return $"{Convert.ToBase64String(aes_key_encrypted)}#{Convert.ToBase64String(data_aes_crypted)}";
        }

        public string SignString(string data)
        {
            var output = private_signature_key.SignData(Encoding.UTF8.GetBytes(data), HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1);
            return Convert.ToBase64String(output);
        }

        public long GetUsedSpace()
        {
            return this.used_space;
        }

        public long GetMaxSpace()
        {
            return this.max_storage_space;
        }

        public long UsedSpaceAdd(long size)
        {
            this.used_space += size;

            return this.used_space;
        }

        public long UsedSpaceSubtract(long size)
        {
            this.used_space -= size;

            return this.used_space;
        }

        private SemaphoreSlim journal_semaphore;

        public async Task<bool> WriteItemToJournalUnsafe(string meta)
        {
            // TODO: add a lock here.. its not the end of the world if two writes intertwine
            //       but it would help eliminate a point of corruption.. no data is lost but
            //       the video might just not show up
            // BUG: see above

            using (var mj = File.Open(this.metajournal_path, FileMode.Append))
            {
                //var meta = Item.Serialize(item);
                var hasher = MD5.Create();

                var hash = BitConverter.ToString(
                    hasher.ComputeHash(
                        Encoding.UTF8.GetBytes(meta)
                    )
                ).Replace("-", "").ToLower();

                byte[] line_bytes = Encoding.UTF8.GetBytes(String.Format("{0}:{1}\n", hash, meta));

                await mj.WriteAsync(line_bytes, 0, line_bytes.Length);
            }

            return true;
        }

        public async Task<bool> WriteItemToJournalAssured(string meta)
        {
            while (true)
            {
                try
                {
                    // Ensure only one write at a time.
                    await journal_semaphore.WaitAsync();

                    if (await WriteItemToJournalUnsafe(meta))
                    {
                        return true;
                    }
                }
                catch (IOException ex)
                {
                    Logger.WriteCriticalString($"WriteItemToJournalAssured had an I/O exception as follows:\n{ex}\nFor:\n{meta}");
                } finally
                {
                    journal_semaphore.Release();
                }

                await Task.Delay(1000);
            }
        }

        public async Task<bool> WriteItemToJournal(JObject item)
        {
            return await WriteItemToJournalAssured(JsonConvert.SerializeObject(item));
        }

        public async Task<bool> WriteItemToJournal(Item item) {

            if (!await WriteItemToJournalAssured(JsonConvert.SerializeObject(item)))
            {
                return false;
            }

            if (items.ContainsKey(item.security_id))
            {
                items[item.security_id] = item;
            }
            else
            {
                items.Add(item.security_id, item);
            }

            return true;
        }

        public async Task<bool> FieldModificationValidForUser(User user, string field) {
            if (!user.admin && !field.Equals("note")) {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Handles any needed work after a successful upload of an item.
        /// </summary>
        public async Task HouseworkAfterUploadSuccess(Item item) {
            if (this.notification_post_url == null) {
                return;
            }

            var req = WebRequest.Create($"{this.notification_post_url}/item-upload-success");

            req.Method = "POST";
            req.ContentType = "text/json";

            var payload = Encoding.UTF8.GetBytes(
                JsonConvert.SerializeObject(item)
            );

            var data = await req.GetRequestStreamAsync();
            
            await data.WriteAsync(payload, 0, payload.Length);
            
            data.Close();

            var resp = await req.GetResponseAsync();
            var data_out = resp.GetResponseStream();
            // No need to read the response stream, unless, we desired to retry
            // on an error.
        }        
    }
}

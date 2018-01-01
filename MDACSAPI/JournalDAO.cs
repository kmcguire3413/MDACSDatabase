using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MDACS.Systems
{
    public class JournalDAO<ItemType>
    {
        public string path { get; }

        public JournalDAO(string path)
        {
            this.path = path;
        }

        public IEnumerable<ItemType> Load() {
            if (!File.Exists(path))
            {
                File.Create(path).Dispose();
            }

            var mj = File.OpenText(path);

            var hasher = MD5.Create();

            long line_no = 0;

            while (!mj.EndOfStream)
            {
                line_no++;

                var line = mj.ReadLine();

                var colon_ndx = line.IndexOf(':');

                if (colon_ndx < 0)
                {
                    Console.WriteLine($"The line {line_no} had no colon.");
                    continue;
                }

                // TODO: add command line option to treat errors below as exceptional conditions and fail

                var hash = line.Substring(0, colon_ndx);
                var meta = line.Substring(colon_ndx + 1).TrimEnd();

                var correct_hash = BitConverter.ToString(
                    hasher.ComputeHash(Encoding.UTF8.GetBytes(meta))
                ).Replace("-", "").ToLower();

                if (hash != correct_hash)
                {
                    Console.WriteLine($"Hash mismatch at line {line_no} for meta:\n{meta}\n File hash is:\n{hash}\n...and the correct hash is:\n{correct_hash}\n.");
                }

                ItemType metaitem;

                try
                {
                    metaitem = JsonConvert.DeserializeObject<ItemType>(meta);
                }
                catch (JsonReaderException ex)
                {
                    Console.WriteLine($"JSON reader exception at line {line_no} thrown:\n{ex}");
                    continue;
                }

                yield return metaitem;
            }

            mj.Dispose();
        }

        public async Task<bool> WriteItemToJournal(string meta)
        {
            using (var mj = File.Open(path, FileMode.Append))
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

        public async Task<bool> WriteItemToJournal(JObject item)
        {
            return await WriteItemToJournal(JsonConvert.SerializeObject(item));
        }

        public async Task<bool> WriteItemToJournal(ItemType item)
        {

            return await WriteItemToJournal(JsonConvert.SerializeObject(item));
        }

    }
}

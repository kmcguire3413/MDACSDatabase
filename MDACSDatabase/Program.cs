#define DOUBLE_ENDED_STREAM_DEBUG

using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using MDACS.API;
using System.Text;
using System.IO;
using static MDACS.API.Auth;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Security.Cryptography.X509Certificates;
using System.Net.Sockets;
using System.Threading;
using System.Net.Security;
using System.Security.Cryptography;
using MDACS.Server;
using static MDACS.API.Database;
using Newtonsoft.Json.Linq;
using MDACSAPI;

namespace MDACS.Database
{
    /// <summary>
    /// A type of exception that all exceptions thrown by this program must derive from. If any exception caught must only be rethrown
    /// if it is embedded as the `caught_exception` property of this class. This can be done by calling the appropriate constructor.
    /// </summary>
    internal class ProgramException: Exception
    {
        public Exception caught_exception { get; }

        public ProgramException(String msg) : base(msg)
        {

        }

        public ProgramException() : base ()
        {

        }

        public ProgramException(String msg, Exception caught_exception) : base(msg)
        {
            this.caught_exception = caught_exception;
        }
    }

    internal class JournalHashException: ProgramException
    {
        public JournalHashException(String msg) : base(msg)
        {
        }
    }

    internal class UnauthorizedException: ProgramException
    {

    }

    internal class InvalidArgumentException : ProgramException
    {

    }


    internal class Helpers
    {
        public static async Task<API.Responses.AuthCheckResponse> ReadMessageFromStreamAndAuthenticate(ServerHandler shandler, int max_size, Stream input_stream)
        {
            var buf = new byte[1024 * 32];
            int pos = 0;

            var a = input_stream.CanRead;
            var b = input_stream.CanTimeout;

            Console.WriteLine("Reading authenticated payload.");

            while (pos < buf.Length)
            {
                var cnt = await input_stream.ReadAsync(buf, pos, buf.Length - pos);

                if (cnt < 1)
                {
                    break;
                }

                pos += cnt;
            }

            Console.WriteLine("Done reading authenticated payload.");

            var buf_utf8_string = Encoding.UTF8.GetString(buf, 0, pos);

            Console.WriteLine(buf_utf8_string);

            var resp = await MDACS.API.Auth.AuthenticateMessageAsync(
                shandler.auth_url,
                buf_utf8_string
            );

            Console.WriteLine("Handing back result from authenticated payload.");

            return resp;
        }

        public static bool CanUserModifyItem(User user, Item item)
        {
            if (user == null || item == null || item.userstr == null)
            {
                return false;
            }

            if (user.admin || user.userfilter == null)
            {
                return true;
            }

            if (item.userstr.IndexOf(user.userfilter) == 0)
            {
                return true;
            }

            return false;
        }

        public static bool CanUserSeeItem(User user, Item item)
        {
            return CanUserModifyItem(user, item);
        }

        public static async Task WriteMessageToStream(Stream output_stream, String out_string)
        {
            var out_buffer = Encoding.UTF8.GetBytes(out_string);
            await output_stream.WriteAsync(out_buffer, 0, out_buffer.Length);
        }
    }

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

        public ServerHandler(
            string metajournal_path,
            string data_path,
            string config_path,
            string auth_url,
            string universal_records_key_path,
            string universal_records_key_pass,
            string universal_records_url,
            int cluster_size,
            long max_storage_space
        )
        {
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
    }

    public class ProgramConfig
    {
        public string metajournal_path;
        public string data_path;
        public string config_path;
        public string auth_url;
        public string ssl_cert_path;
        public string ssl_cert_pass;
        public string universal_records_key_path;
        public string universal_records_key_pass;
        public string universal_records_url;
        public ushort port;
    }

    public class Program
    {
        public static event Func<JObject, bool> logger_output_base;

        public static void LoggerOutput(JObject item)
        {
            // Allow our default logger output to be hooked _and_ overriden if desired.
            if (logger_output_base != null && logger_output_base(item) == true)
            {
                return;
            }

            if (item["type"].ToObject<string>()?.Equals("string") == true)
            {
                var msg = item["value"].ToObject<string>();
                var source = item["stack"].ToObject<string[]>();

                Console.WriteLine($"{source[0]}: {msg}");
            }
        }

        public static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Provide path or file that contains the JSON configuration. If file does not exit then default one will be created.");
                return;
            }

            // Attach a handler for logger events.
            Logger.handler_event += LoggerOutput;

            if (!File.Exists(args[0]))
            {
                ProgramConfig defcfg = new ProgramConfig {
                    metajournal_path = "The file path to the metadata journal.",
                    data_path = "The path to the directory containing the data files backing the journal.",
                    config_path = "The path to the directory holding device configuration files.",
                    auth_url = "The HTTP or HTTPS URL to the authentication service.",
                    ssl_cert_path = "The PFX file that contains both the private and public keys for communications.",
                    universal_records_key_path = "The PFX file for the universal records system.",
                    port = 34001,
                };

                var defcfgfp = File.CreateText(args[0]);
                defcfgfp.Write(JsonConvert.SerializeObject(defcfg, Formatting.Indented));
                defcfgfp.Dispose();

                Console.WriteLine("Default configuration created at location specified.");
                return;
            }

            var cfgfp = File.OpenText(args[0]);

            var cfg = JsonConvert.DeserializeObject<ProgramConfig>(cfgfp.ReadToEnd());

            cfgfp.Dispose();

            var handler = new ServerHandler(
                metajournal_path: cfg.metajournal_path,
                data_path: cfg.data_path,
                config_path: cfg.config_path,
                auth_url: cfg.auth_url,
                cluster_size: 4096,
                max_storage_space: (long)1024 * 1024 * 1024 * 680,
                universal_records_key_path: cfg.universal_records_key_path,
                universal_records_key_pass: cfg.universal_records_key_pass,
                universal_records_url: cfg.universal_records_url
            );

            //var server = new HTTPServer<ServerHandler>(handler, cfg.ssl_cert_path, cfg.ssl_cert_pass);

            var handlers = new Dictionary<String, SimpleServer<ServerHandler>.SimpleHTTPHandler>();

            handlers.Add("/upload", HandleUpload.Action);
            handlers.Add("/device-config", HandleDeviceConfig.Action);
            handlers.Add("/commit_batch_single_ops", HandleBatchSingleOps.Action);
            handlers.Add("/download", HandleDownload.Action);
            handlers.Add("/enumerate-configurations", HandleEnumerateConfigurations.Action);
            handlers.Add("/data", HandleData.Action);
            handlers.Add("/commitset", HandleCommitSet.Action);
            handlers.Add("/commit-configuration", HandleCommitConfiguration.Action);
            handlers.Add("/delete", HandleDelete.Action);
            handlers.Add("/spaceinfo", HandleSpaceInfo.Action);

            var server = SimpleServer<ServerHandler>.Create(
                handler,
                handlers,
                cfg.port,
                cfg.ssl_cert_path,
                cfg.ssl_cert_pass
            );

            var a = new Thread(() =>
            {
                server.Wait();
            });


            a.Start();
            a.Join();


            // Please do not let me forget this convulted retarded sequence to get from PEM to PFX with the private key.
            // openssl crl2pkcs7 -nocrl -inkey privkey.pem -certfile fullchain.pem -out test.p7b
            // openssl pkcs7 -print_certs -in test.p7b -out test.cer
            // openssl pkcs12 -export -in test.cer -inkey privkey.pem -out test.pfx -nodes
            // THEN... for Windows, at least, import into cert store, then export with private key and password.
            // FINALLY... use the key now and make sure its X509Certificate2.. notice the 2 on the end? Yep.
        }
    }
}

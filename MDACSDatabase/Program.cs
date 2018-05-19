#define DOUBLE_ENDED_STREAM_DEBUG

using System;
using System.Net;
using System.Net.Http;
using MDACS.API;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Net.Sockets;
using System.Threading;
using System.Net.Security;
using MDACS.Server;
using Newtonsoft.Json.Linq;
using MDACSAPI;
using System.Diagnostics;

namespace MDACS.Database
{
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

                Debug.WriteLine($"{source[0]}: {msg}");
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
                    notification_url = "Use null or the URL for the notification service.",
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
                universal_records_url: cfg.universal_records_url,
                notification_post_url: cfg.notification_url
            );

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
            handlers.Add("/version", HandleVersion.Action);
            handlers.Add("/", HandleLocalWebRes.Index);
            handlers.Add("/utility", HandleLocalWebRes.Utility);
            handlers.Add("/get-config", HandleConfigRequest.Action);

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

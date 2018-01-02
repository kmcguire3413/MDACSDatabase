﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MDACS.API
{
    namespace Requests
    {
        public class BatchSingleOp
        {
            public string sid;
            public string field_name;
            public JToken value;
        }

        public class HandleBatchSingleOpsRequest
        {
            public BatchSingleOp[] ops;
        }

        public class UploadHeader
        {
            public string datestr;
            public string timestr;
            public string devicestr;
            public string userstr;
            public string datatype;
            public ulong datasize;
        }

        public class CommitSetRequest
        {
            public string security_id;
            public JObject meta;
        }

        public class UniversalPullInfoRequest
        {
            public string data_hash_sha512;
        }
    }

    namespace Responses
    {
        public class HandleBatchSingleOpsResponse
        {
            public bool success;
            public Requests.BatchSingleOp[] failed;
        }

        public class UniversalPullInfoResponse
        {
            public bool success;
            public UniversalRecords.UniversalRecordItem[] basic;
        }

        public class UniversalPushInfoResponse
        {
            public bool success;
        }

        public class UniversalRegistrationResponse
        {
            public bool success;
        }

        public class CommitSetResponse
        {
            public bool success;
        }

        public class UploadResponse
        {
            public bool success;
            public string security_id;
            public string fqpath;
        }

        public class DataResponse
        {
            public Database.Alert[] alerts;
            public Database.Item[] data;
        }

        public class AuthResponse
        {
            public string challenge;
        }

        public class AuthCheckResponse
        {
            public bool success;
            public string payload;
            public Auth.User user;
        }
    }

    public class Session
    {
        public string auth_url { get; }
        public string db_url { get;  }
        public string username { get; }
        public string password { get; }

        public Session(
            string auth_url,
            string db_url,
            string username,
            string password
            )
        {
            this.auth_url = auth_url;
            this.db_url = db_url;
            this.username = username;
            this.password = password;
        }

        public async Task<Responses.CommitSetResponse> CommitSetAsync(string sid, JObject meta)
        {
            return await Database.CommitSetAsync(
                auth_url,
                db_url,
                username,
                password,
                sid,
                meta
            );
        }

        public async Task<Responses.HandleBatchSingleOpsResponse> BatchSingleOps(Requests.BatchSingleOp[] ops)
        {
            return await Database.BatchSingleOps(
                auth_url,
                db_url,
                username,
                password,
                ops
            );
        }

        public async Task<Responses.DataResponse> Data()
        {
            return await Database.GetDataAsync(
                auth_url,
                db_url,
                username,
                password,
                null
            );
        }

        public async Task<Responses.UploadResponse> UploadAsync(
            long datasize,
            string datatype,
            string datestr,
            string devicestr,
            string timestr,
            string userstr,
            Stream data
        )
        {
            return await Database.UploadAsync(
                auth_url,
                db_url,
                username,
                password,
                datasize,
                datatype,
                datestr,
                devicestr,
                timestr,
                userstr,
                data
            );
        }
    }

    public static class UniversalRecords
    {
        public sealed class UniversalRecordRegistration
        {
            public string uuid;
            public string public_key;
            public string organization;
            public string department;
            public string email;
            public string phone;
            public string contact_name;
            public string uuid_extra;
            public string signature;

            public void IncludePublicKeyFrom(RSA rsa)
            {
                var param = rsa.ExportParameters(false);
                var modulus = Convert.ToBase64String(param.Modulus);
                var exponent = Convert.ToBase64String(param.Exponent);

                public_key = $"v1-rsa#{modulus}#{exponent}";
            }

            public RSA LoadRSAKey()
            {
                var param = new RSAParameters();
                var parts = public_key.Split('#');

                if (parts.Length < 3 || !parts[0].Equals("v1-rsa"))
                {
                    throw new NotImplementedException("The key type has not been implemented.");
                }

                var modulus = Convert.FromBase64String(parts[1]);
                var exponent = Convert.FromBase64String(parts[2]);

                param.Modulus = modulus;
                param.Exponent = exponent;

                var rsa = RSA.Create();

                rsa.ImportParameters(param);

                return rsa;
            }

            public bool VerifyItem(UniversalRecordItem item)
            {
                var rsa = LoadRSAKey();

                byte[] data_bytes;
                byte[] signature_bytes;

                var tmp = item.signature;

                try
                {
                    // This is why this class has been sealed. So we know what is expected by setting signature to a blank string.
                    item.signature = "";
                    data_bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(item));
                    signature_bytes = Convert.FromBase64String(tmp);
                }
                finally
                {
                    item.signature = tmp;
                }

                return rsa.VerifyData(data_bytes, signature_bytes, HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1);
            }

            public void Sign(RSA rsa)
            {
                signature = "";

                var data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(this));
                var sigbytes = rsa.SignData(data, HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1);

                signature = Convert.ToBase64String(sigbytes);
            }

            public bool VerifySelfSignature()
            {
                var rsa = LoadRSAKey();

                var tmp = signature;

                try
                {
                    signature = "";

                    var data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(this));
                    var sig_bytes = Convert.FromBase64String(tmp);
                    
                    if (rsa.VerifyData(data, sig_bytes, HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1))
                    {
                        return true;
                    }
                } finally
                {
                    signature = tmp;
                }

                return false;
            }
        }

        public sealed class UniversalRecordItem
        {
            public string uuid;
            public string data_hash_sha512;
            public string uuid_extension_data;
            public string signature;

            public void Sign(RSA rsa)
            {
                signature = "";

                var data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(this));
                var sigbytes = rsa.SignData(data, HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1);
                signature = Convert.ToBase64String(sigbytes);
            }
        }

        public static async Task<string> TransactionAsync(string universal_url, string msg)
        {
            return await Auth.AuthTransactionAsync(universal_url, msg);
        }

        public static async Task<Responses.UniversalPullInfoResponse> PullInfo(string universal_url, string data_hash_512)
        {
            var req = new Requests.UniversalPullInfoRequest()
            {
                data_hash_sha512 = data_hash_512,
            };

            var msg = JsonConvert.SerializeObject(req);

            return JsonConvert.DeserializeObject<Responses.UniversalPullInfoResponse>(
                await TransactionAsync(
                    $"{universal_url}/v1/pullinfo",
                    msg
            ));
        }

        public static async Task<Responses.UniversalPushInfoResponse> PushInfo(string universal_url, UniversalRecordItem item)
        {
            var msg = JsonConvert.SerializeObject(item);

            return JsonConvert.DeserializeObject<Responses.UniversalPushInfoResponse>(
                await TransactionAsync(
                    $"{universal_url}/v1/pushinfo",
                    msg
            ));
        }

        public static async Task<Responses.UniversalRegistrationResponse> Register(string universal_url, UniversalRecordRegistration reg)
        {
            var msg = JsonConvert.SerializeObject(reg);

            return JsonConvert.DeserializeObject<Responses.UniversalRegistrationResponse>(
                await TransactionAsync(
                    $"{universal_url}/v1/register", 
                    msg
            ));
        }
    }

    public class Database
    {
        private static async Task<Stream> ReadStreamTransactionAsync(
            string auth_url,
            string db_url,
            string username,
            string password,
            string payload
        )
        {
            WebRequest req;

            // Leave synchronous for this moment.
            var payload_bytes = Encoding.ASCII.GetBytes(
                await Auth.BuildAuthWithPayloadAsync(auth_url, username, password, payload)
            );

            req = WebRequest.Create(db_url);

            req.Method = "POST";
            req.ContentType = "text/json";

            var data = await req.GetRequestStreamAsync();
            await data.WriteAsync(payload_bytes, 0, payload_bytes.Length);
            data.Close();

            var resp = await req.GetResponseAsync();

            var data_out = resp.GetResponseStream();

            return data_out;
        }

        private static Stream ReadStreamTransaction(
            string auth_url,
            string db_url,
            string username,
            string password,
            string payload
        )
        {
            WebRequest req;
            WebResponse resp;
            Stream data;

            var tsk = Auth.BuildAuthWithPayloadAsync(auth_url, username, password, payload);

            tsk.Wait();

            var payload_bytes = Encoding.ASCII.GetBytes(
                tsk.Result
            );

            req = WebRequest.Create(db_url);

            req.Method = "POST";
            req.ContentType = "text/json";

            data = req.GetRequestStream();
            data.Write(payload_bytes, 0, payload_bytes.Length);
            data.Close();

            resp = req.GetResponse();

            data = resp.GetResponseStream();

            return data;
        }

        private static string StringTransaction(
            string auth_url, 
            string db_url, 
            string username, 
            string password,
            string payload
        )
        {
            WebRequest req;
            WebResponse resp;
            Stream data;
            StreamReader reader;

            var tsk = Auth.BuildAuthWithPayloadAsync(auth_url, username, password, payload);

            tsk.Wait();

            var payload_bytes = Encoding.ASCII.GetBytes(
                tsk.Result
            );

            req = WebRequest.Create(db_url);

            req.Method = "POST";
            req.ContentType = "text/json";

            data = req.GetRequestStream();
            data.Write(payload_bytes, 0, payload_bytes.Length);
            data.Close();

            resp = req.GetResponse();

            data = resp.GetResponseStream();

            reader = new StreamReader(data);

            return reader.ReadToEnd();
        }

        public class Alert
        {

        }

        public enum ItemSourceType
        {
            AmazonGlacier = 0,
            CIFSPrivateNetwork = 1,
        }

        public class Item
        {
            public string security_id;
            public string node;
            public double duration;
            public double metatime;
            public string fqpath;
            public string userstr;
            public string timestr;
            public string datestr;
            public string devicestr;
            public string datatype;
            public ulong datasize;
            public string note;
            public string state;
            public string uploaded_by_user;
            public string data_hash_sha512;
            public string manager_uuid;

            public static string Serialize(Item item)
            {
                return JsonConvert.SerializeObject(item);
            }

            public static Item Deserialize(string input)
            {
                return JsonConvert.DeserializeObject<Item>(input);
            }
        }

        public static async Task<Stream> DownloadDataAsync(string security_id, string auth_url, string db_url, string username, string password)
        {
            return await ReadStreamTransactionAsync(
                auth_url,
                string.Format("{0}/download?{1}", db_url, security_id),
                username,
                password,
                "{}"
            );
        }

        public static Stream DownloadData(string security_id, string auth_url, string db_url, string username, string password)
        {
            return ReadStreamTransaction(
                auth_url,
                string.Format("{0}/download?{1}", db_url, security_id),
                username,
                password,
                "{}"
            );
        }

        public static async Task<Responses.HandleBatchSingleOpsResponse> BatchSingleOps(
            string auth_url,
            string db_url,
            string username,
            string password,
            Requests.BatchSingleOp[] ops)
        {
            var req = new Requests.HandleBatchSingleOpsRequest()
            {
                ops = ops
            };

            var stream = await ReadStreamTransactionAsync(
                auth_url,
                $"{db_url}/commit_batch_single_ops",
                username,
                password,
                JsonConvert.SerializeObject(req)
            );

            var bs = new StreamReader(stream, Encoding.UTF8, false);

            return JsonConvert.DeserializeObject<Responses.HandleBatchSingleOpsResponse>(await bs.ReadToEndAsync());
        }

        public static async Task<Responses.CommitSetResponse> CommitSetAsync(
            string auth_url, 
            string db_url, 
            string username, 
            string password, 
            string sid, 
            JObject meta)
        {
            var csreq = new Requests.CommitSetRequest();

            csreq.security_id = sid;
            csreq.meta = meta;

            var stream = await ReadStreamTransactionAsync(
                auth_url,
                $"{db_url}/commitset",
                username,
                password,
                JsonConvert.SerializeObject(csreq)
            );

            var bs = new StreamReader(stream, Encoding.UTF8, false);

            return JsonConvert.DeserializeObject<Responses.CommitSetResponse>(await bs.ReadToEndAsync());
        }

        public static async Task<Responses.UploadResponse> UploadAsync(
            string auth_url,
            string db_url,
            string username,
            string password,
            long datasize,
            string datatype,
            string datestr,
            string devicestr,
            string timestr,
            string userstr,
            Stream data
        ) {
            var header = new Requests.UploadHeader();

            header.datasize = (ulong)datasize;
            header.datatype = datatype;
            header.datestr = datestr;
            header.devicestr = devicestr;
            header.timestr = timestr;
            header.userstr = userstr;

            var payload = JsonConvert.SerializeObject(header);

            var packet = await API.Auth.BuildAuthWithPayloadAsync(
                auth_url,
                username,
                password,
                payload
            );
            var packet_bytes = Encoding.UTF8.GetBytes($"{packet}\n");

            var wr = WebRequest.Create($"{db_url}/upload");

            wr.ContentType = "text/json";
            wr.Method = "POST";

            ((HttpWebRequest)wr).AllowWriteStreamBuffering = false;
            ((HttpWebRequest)wr).SendChunked = true;
            // When sending a very long post this might need to be turned off
            // since it can cause an abrupt canceling of the request.
            ((HttpWebRequest)wr).KeepAlive = false;

            var reqstream = await wr.GetRequestStreamAsync();

            await reqstream.WriteAsync(packet_bytes, 0, packet_bytes.Length);

            await data.CopyToAsync(reqstream);

            reqstream.Close();

            var chunk = new byte[1024];

            var resp = await wr.GetResponseAsync();
            var rstream = resp.GetResponseStream();
            var resp_length = await rstream.ReadAsync(chunk, 0, chunk.Length);
            var resp_text = Encoding.UTF8.GetString(chunk, 0, resp_length);

            return JsonConvert.DeserializeObject<Responses.UploadResponse>(resp_text);
        }

        public static async Task<Responses.UploadResponse> UploadAsync(
            string auth_url,
            string db_url,
            string username,
            string password,
            long datasize,
            string datatype,
            string datestr,
            string devicestr,
            string timestr,
            string userstr,
            IEnumerable<byte[]> callback
        )
        {
            var header = new Requests.UploadHeader();

            header.datasize = (ulong)datasize;
            header.datatype = datatype;
            header.datestr = datestr;
            header.devicestr = devicestr;
            header.timestr = timestr;
            header.userstr = userstr;

            var payload = JsonConvert.SerializeObject(header);

            var packet = await API.Auth.BuildAuthWithPayloadAsync(
                auth_url, 
                username, 
                password,
                payload
            );
            var packet_bytes = Encoding.UTF8.GetBytes($"{packet}\n");

            var wr = WebRequest.Create($"{db_url}/upload");

            wr.ContentType = "text/json";
            wr.Method = "POST";

            ((HttpWebRequest)wr).AllowWriteStreamBuffering = false;
            ((HttpWebRequest)wr).SendChunked = true;
            // When sending a very long post this might need to be turned off
            // since it can cause an abrupt canceling of the request.
            ((HttpWebRequest)wr).KeepAlive = false;

            var reqstream = await wr.GetRequestStreamAsync();

            await reqstream.WriteAsync(packet_bytes, 0, packet_bytes.Length);

            foreach (var stream_chunk in callback)
            {
                await reqstream.WriteAsync(stream_chunk, 0, stream_chunk.Length);
            }

            reqstream.Close();

            var chunk = new byte[1024];

            var resp = await wr.GetResponseAsync();
            var rstream = resp.GetResponseStream();
            var resp_length = await rstream.ReadAsync(chunk, 0, chunk.Length);
            var resp_text = Encoding.UTF8.GetString(chunk, 0, resp_length);

            return JsonConvert.DeserializeObject<Responses.UploadResponse>(resp_text);
        }

        public delegate void GetDataProgress(ulong bytes_read);

        public static async Task<Responses.DataResponse> GetDataAsync(string auth_url, string db_url, string username, string password, GetDataProgress progress_event)
        {
            string payload = "{}";

            var stream = await ReadStreamTransactionAsync(
                auth_url,
                string.Format("{0}/data", db_url),
                username,
                password,
                payload
            );

            MemoryStream ms = new MemoryStream(1024 * 1024 * 5);

            int count = 0;
            byte[] buf = new byte[1024 * 32];
            ulong amount_read = 0;

            while ((count = await stream.ReadAsync(buf, 0, buf.Length)) > 0)
            {
                ms.Write(buf, 0, count);
                amount_read += (ulong)count;
                if (progress_event != null)
                {
                    progress_event?.Invoke(amount_read);
                }
            }

            string resp_json = Encoding.UTF8.GetString(ms.ToArray());

            //string resp_json = File.ReadAllText("dump.txt");

            return JsonConvert.DeserializeObject<Responses.DataResponse>(resp_json);
        }

        public static Responses.DataResponse GetData(string auth_url, string db_url, string username, string password, GetDataProgress progress_event)
        {
            string payload = "{}";

            var stream = ReadStreamTransaction(
                auth_url,
                string.Format("{0}/data", db_url),
                username,
                password,
                payload
            );

            MemoryStream ms = new MemoryStream(1024 * 1024 * 5);

            int count = 0;
            byte[] buf = new byte[1024 * 32];
            ulong amount_read = 0;

            while ((count = stream.Read(buf, 0, buf.Length)) > 0)
            {
                ms.Write(buf, 0, count);
                amount_read += (ulong)count;
                if (progress_event != null) {
                    progress_event?.Invoke(amount_read);
                }
            }

            string resp_json = Encoding.UTF8.GetString(ms.ToArray());

            //string resp_json = File.ReadAllText("dump.txt");

            return JsonConvert.DeserializeObject<Responses.DataResponse>(resp_json);
        }
    }

    public class Auth
    {
        class AuthCompletePacketInner
        {
            public string challenge;
            public string chash;
            public bool payload;
        }

        class AuthCompletePacket
        {
            public AuthCompletePacketInner auth;
            public string payload;
        }

        private static string ByteArrayToHexString(byte[] input)
        {
            StringBuilder sb;

            sb = new StringBuilder();

            for (uint x = 0; x < input.Length; ++x)
            {
                sb.AppendFormat("{0:x2}", input[x]);
            }

            return sb.ToString();
        }

        public static async Task<string> AuthTransactionAsync(string auth_url, string msg)
        {
            WebRequest req;
            WebResponse resp;
            Stream data;
            StreamReader reader;

            Console.WriteLine(string.Format("creating web request to {0}", auth_url));

            req = WebRequest.Create(auth_url);

            req.Method = "POST";
            req.ContentType = "text/json";

            Console.WriteLine("getting request stream");

            data = await req.GetRequestStreamAsync();
            var buf = Encoding.UTF8.GetBytes(msg);
            await data.WriteAsync(buf, 0, buf.Length);

            Console.WriteLine("closing request stream");
            data.Close();

            Console.WriteLine("getting response");
            resp = await req.GetResponseAsync();

            Console.WriteLine("got response");

            data = resp.GetResponseStream();

            Console.WriteLine("got response stream");

            reader = new StreamReader(data);

            Console.WriteLine("reading response until end by returning data");

            return await reader.ReadToEndAsync();
        }

        public static async Task<string> GetAuthChallengeAsync(string auth_url)
        {
            WebRequest req;
            WebResponse resp;
            Stream data;
            StreamReader reader;
            string json;
            Responses.AuthResponse auth_resp;

            req = WebRequest.Create(string.Format("{0}/challenge", auth_url));

            req.Method = "POST";
            req.ContentType = "text/json";
            req.ContentLength = 0;

            data = await req.GetRequestStreamAsync();
            data.Close();

            resp = await req.GetResponseAsync();

            data = resp.GetResponseStream();

            reader = new StreamReader(data);

            json = reader.ReadToEnd();

            auth_resp = JsonConvert.DeserializeObject<Responses.AuthResponse>(json);

            return auth_resp.challenge;
        }

        public class MsgAuth
        {
            public string chash;
            public string challenge;
            public string hash;
        }

        public class Msg
        {
            public MsgAuth auth;
            public string payload;
        }

        public class User
        {
            public string name;
            public string user;
            public string hash;
            public bool admin;
            public string userfilter;
            public bool can_delete;
        }

        public class AuthCheckPayload
        {
            public string chash;
            public string phash;
            public string challenge;
        }

        public class AuthCheckNoPayload
        {
            public string hash;
            public string challenge;
        }

        public static async Task<Responses.AuthCheckResponse> AuthenticateMessageAsync(string auth_url, string msg)
        {
            var msg_decoded = JsonConvert.DeserializeObject<Msg>(msg);

            if (msg_decoded == null)
            {
                Console.WriteLine("msg_decoded was null");
                var tmp = new Responses.AuthCheckResponse();
                tmp.success = false;
                return tmp;
            }

            Console.WriteLine("msg was decoded");

            return await AuthenticateMessageAsync(
                auth_url,
                msg_decoded
            );
        }

        public static async Task<Responses.AuthCheckResponse> AuthenticateMessageAsync(string auth_url, Msg msg)
        {
            if (msg.payload == null) {
                Console.WriteLine("authenticating message with no payload");
                var checknp = new AuthCheckNoPayload();

                checknp.hash = msg.auth.hash;
                checknp.challenge = msg.auth.challenge;

                var resp_string = await AuthTransactionAsync(
                    string.Format("{0}/verify", auth_url),
                    JsonConvert.SerializeObject(checknp)
                );

                Console.WriteLine("got verify result");

                return JsonConvert.DeserializeObject<Responses.AuthCheckResponse>(resp_string);
            }

            Console.WriteLine("authenticating message with payload");

            var check = new AuthCheckPayload();

            var hasher = new SHA512Managed();

            var binary_hash = hasher.ComputeHash(Encoding.UTF8.GetBytes(msg.payload));

            var phash = ByteArrayToHexString(binary_hash);

            check.chash = msg.auth.chash;
            check.phash = phash;
            check.challenge = msg.auth.challenge;

            Console.WriteLine("doing actual verify-payload call");

            var resp_string2 = await AuthTransactionAsync(
                string.Format("{0}/verify-payload", auth_url),
                JsonConvert.SerializeObject(check)
            );

            Console.WriteLine("returning results");

            var resp = JsonConvert.DeserializeObject<Responses.AuthCheckResponse>(resp_string2);

            resp.payload = msg.payload;

            return resp;
        }

        public static async Task<string> BuildAuthWithPayloadAsync(string auth_url, string username, string password, string payload)
        {
            var challenge = await GetAuthChallengeAsync(auth_url);
            var hasher = new SHA512Managed();

            var payload_hash = hasher.ComputeHash(Encoding.ASCII.GetBytes(payload));
            var password_hash = hasher.ComputeHash(Encoding.ASCII.GetBytes(password));

            var complete_hash = hasher.ComputeHash(
                Encoding.ASCII.GetBytes(string.Format(
                    "{0}{1}{2}{3}",
                    ByteArrayToHexString(payload_hash),
                    challenge,
                    username,
                    ByteArrayToHexString(password_hash)
                ))
            );

            var packet = new AuthCompletePacket();
            packet.auth = new AuthCompletePacketInner();

            packet.auth.challenge = challenge;
            packet.auth.chash = ByteArrayToHexString(complete_hash);
            packet.auth.payload = true;
            packet.payload = payload;

            return JsonConvert.SerializeObject(packet);
        }
    }
}

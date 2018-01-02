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
        public class UploadHeader
        {
            public String datestr;
            public String timestr;
            public String devicestr;
            public String userstr;
            public String datatype;
            public ulong datasize;
        }

        public class CommitSetRequest
        {
            public String security_id;
            public JObject meta;
        }
    }

    namespace Responses
    {
        public class CommitSetResponse
        {
            public bool success;
        }

        public struct UploadResponse
        {
            public bool success;
            public String security_id;
            public String fqpath;
        }

        public struct DataResponse
        {
            public Database.Alert[] alerts;
            public Database.DataItem[] data;
        }

        struct AuthResponse
        {
            public string challenge;
        }

        public class AuthCheckResponse
        {
            public bool success;
            public String payload;
            public Auth.User user;
        }
    }

    public class Session
    {
        public String auth_url { get; }
        public String db_url { get;  }
        public String username { get; }
        public String password { get; }

        public Session(
            String auth_url,
            String db_url,
            String username,
            String password
            )
        {
            this.auth_url = auth_url;
            this.db_url = db_url;
            this.username = username;
            this.password = password;
        }

        public async Task<Responses.CommitSetResponse> CommitSetAsync(String sid, JObject meta)
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

        public async Task<Responses.UploadResponse> UploadAsync(
            long datasize,
            String datatype,
            String datestr,
            String devicestr,
            String timestr,
            String userstr,
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

        public struct Alert
        {

        }

        public class DataItem
        {
            public string security_id;
            public string node;
            public string datatype;
            public string datestr;
            public string userstr;
            public string devicestr;
            public string timestr;
            public string note;
            public ulong datasize;
            public float duration;
            public string state;
        }

        public static async Task<Stream> DownloadDataAsync(string security_id, string auth_url, string db_url, string username, string password)
        {
            return await ReadStreamTransactionAsync(
                auth_url,
                String.Format("{0}/download?{1}", db_url, security_id),
                username,
                password,
                "{}"
            );
        }

        public static Stream DownloadData(string security_id, string auth_url, string db_url, string username, string password)
        {
            return ReadStreamTransaction(
                auth_url,
                String.Format("{0}/download?{1}", db_url, security_id),
                username,
                password,
                "{}"
            );
        }

        public static async Task<Responses.CommitSetResponse> CommitSetAsync(
            String auth_url, 
            String db_url, 
            String username, 
            String password, 
            String sid, 
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
            String auth_url,
            String db_url,
            String username,
            String password,
            long datasize,
            String datatype,
            String datestr,
            String devicestr,
            String timestr,
            String userstr,
            Stream data
        )
        {
            if (data.Length - data.Position != (long)datasize)
            {
                throw new ArgumentException("The data stream must be exactly the specified length from its current position as `datasize`.");
            }

            var header = new Requests.UploadHeader();

            header.datasize = (ulong)datasize;
            header.datatype = datatype;
            header.datestr = datestr;
            header.devicestr = devicestr;
            header.timestr = timestr;
            header.userstr = userstr;

            var packet = API.Auth.BuildAuthWithPayloadAsync(
                auth_url, 
                username, 
                password, 
                JsonConvert.SerializeObject(header)
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

            var chunk = new byte[1024];

            await data.CopyToAsync(reqstream);

            reqstream.Close();

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
                String.Format("{0}/data", db_url),
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
                String.Format("{0}/data", db_url),
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
        struct AuthCompletePacketInner
        {
            public string challenge;
            public string chash;
            public bool payload;
        }

        struct AuthCompletePacket
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

        public static async Task<String> AuthTransactionAsync(String auth_url, String msg)
        {
            WebRequest req;
            WebResponse resp;
            Stream data;
            StreamReader reader;

            Console.WriteLine(String.Format("creating web request to {0}", auth_url));

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

        public static async Task<String> GetAuthChallengeAsync(string auth_url)
        {
            WebRequest req;
            WebResponse resp;
            Stream data;
            StreamReader reader;
            string json;
            Responses.AuthResponse auth_resp;

            req = WebRequest.Create(String.Format("{0}/challenge", auth_url));

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
            public String payload;
        }

        public class User
        {
            public String name;
            public String user;
            public String hash;
            public bool admin;
            public String userfiler;
            public bool can_delete;
        }

        public class AuthCheckPayload
        {
            public String chash;
            public String phash;
            public String challenge;
        }

        public class AuthCheckNoPayload
        {
            public String hash;
            public String challenge;
        }

        public static async Task<Responses.AuthCheckResponse> AuthenticateMessageAsync(String auth_url, String msg)
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

        public static async Task<Responses.AuthCheckResponse> AuthenticateMessageAsync(String auth_url, Msg msg)
        {
            if (msg.payload == null) {
                Console.WriteLine("authenticating message with no payload");
                var checknp = new AuthCheckNoPayload();

                checknp.hash = msg.auth.hash;
                checknp.challenge = msg.auth.challenge;

                var resp_string = await AuthTransactionAsync(
                    String.Format("{0}/verify", auth_url),
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
                String.Format("{0}/verify-payload", auth_url),
                JsonConvert.SerializeObject(check)
            );

            Console.WriteLine("returning results");

            var resp = JsonConvert.DeserializeObject<Responses.AuthCheckResponse>(resp_string2);

            resp.payload = msg.payload;

            return resp;
        }

        public static async Task<String> BuildAuthWithPayloadAsync(string auth_url, string username, string password, string payload)
        {
            var challenge = await GetAuthChallengeAsync(auth_url);
            var hasher = new SHA512Managed();

            var payload_hash = hasher.ComputeHash(Encoding.ASCII.GetBytes(payload));
            var password_hash = hasher.ComputeHash(Encoding.ASCII.GetBytes(password));

            var complete_hash = hasher.ComputeHash(
                Encoding.ASCII.GetBytes(String.Format(
                    "{0}{1}{2}{3}",
                    ByteArrayToHexString(payload_hash),
                    challenge,
                    username,
                    ByteArrayToHexString(password_hash)
                ))
            );

            AuthCompletePacket packet;
            packet.auth.challenge = challenge;
            packet.auth.chash = ByteArrayToHexString(complete_hash);
            packet.auth.payload = true;
            packet.payload = payload;

            return JsonConvert.SerializeObject(packet);
        }
    }
}

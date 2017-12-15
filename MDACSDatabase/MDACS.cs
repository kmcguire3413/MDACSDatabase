using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MDACS.API
{
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
                Auth.BuildAuthWithPayload(auth_url, username, password, payload)
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

            var payload_bytes = Encoding.ASCII.GetBytes(
                Auth.BuildAuthWithPayload(auth_url, username, password, payload)
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

            var payload_bytes = Encoding.ASCII.GetBytes(
                Auth.BuildAuthWithPayload(auth_url, username, password, payload)
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

        public struct DataResponse
        {
            public Alert[] alerts;
            public DataItem[] data;
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

        public delegate void GetDataProgress(ulong bytes_read);

        public static async Task<DataResponse> GetDataAsync(string auth_url, string db_url, string username, string password, GetDataProgress progress_event)
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

            return JsonConvert.DeserializeObject<DataResponse>(resp_json);
        }

        public static DataResponse GetData(string auth_url, string db_url, string username, string password, GetDataProgress progress_event)
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

            return JsonConvert.DeserializeObject<DataResponse>(resp_json);
        }
    }

    public class Auth
    {
        struct AuthResponse
        {
            public string challenge;
        }

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

            req = WebRequest.Create(auth_url);

            req.Method = "POST";
            req.ContentType = "text/json";

            data = req.GetRequestStream();
            var buf = Encoding.UTF8.GetBytes(msg);
            await data.WriteAsync(buf, 0, buf.Length);
            data.Close();

            resp = req.GetResponse();

            data = resp.GetResponseStream();

            reader = new StreamReader(data);

            return reader.ReadToEnd();
        }

        public static string GetAuthChallenge(string auth_url)
        {
            WebRequest req;
            WebResponse resp;
            Stream data;
            StreamReader reader;
            string json;
            AuthResponse auth_resp;

            req = WebRequest.Create(String.Format("{0}/challenge", auth_url));

            req.Method = "POST";
            req.ContentType = "text/json";
            req.ContentLength = 0;

            data = req.GetRequestStream();
            data.Close();

            resp = req.GetResponse();

            data = resp.GetResponseStream();

            reader = new StreamReader(data);

            json = reader.ReadToEnd();

            auth_resp = JsonConvert.DeserializeObject<AuthResponse>(json);

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

        public class AuthCheckResponse
        {
            public bool success;
            public String payload;
            public User user;
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

        public static async Task<AuthCheckResponse> AuthenticateMessageAsync(String auth_url, String msg)
        {
            var msg_decoded = JsonConvert.DeserializeObject<Msg>(msg);

            if (msg_decoded == null)
            {
                var tmp = new AuthCheckResponse();
                tmp.success = false;
                return tmp;
            }

            return await AuthenticateMessageAsync(
                auth_url,
                msg_decoded
            );
        }

        public static async Task<AuthCheckResponse> AuthenticateMessageAsync(String auth_url, Msg msg)
        {
            if (msg.payload == null) {
                var checknp = new AuthCheckNoPayload();

                checknp.hash = msg.auth.hash;
                checknp.challenge = msg.auth.challenge;

                var resp_string = await AuthTransactionAsync(
                    String.Format("{0}/verify", auth_url),
                    JsonConvert.SerializeObject(checknp)
                );

                return JsonConvert.DeserializeObject<AuthCheckResponse>(resp_string);
            }

            var check = new AuthCheckPayload();

            var hasher = new SHA512Managed();

            var binary_hash = hasher.ComputeHash(Encoding.UTF8.GetBytes(msg.payload));

            var phash = ByteArrayToHexString(binary_hash);

            check.chash = msg.auth.chash;
            check.phash = phash;
            check.challenge = msg.auth.challenge;

            var resp_string2 = await AuthTransactionAsync(
                String.Format("{0}/verify-payload", auth_url),
                JsonConvert.SerializeObject(check)
            );

            return JsonConvert.DeserializeObject<AuthCheckResponse>(resp_string2);
        }

        public static string BuildAuthWithPayload(string auth_url, string username, string password, string payload)
        {
            string challenge;
            SHA512 hasher;
            byte[] payload_hash;
            byte[] password_hash;
            byte[] complete_hash;
            AuthCompletePacket packet;

            challenge = GetAuthChallenge(auth_url);

            hasher = new SHA512Managed();

            payload_hash = hasher.ComputeHash(Encoding.ASCII.GetBytes(payload));
            password_hash = hasher.ComputeHash(Encoding.ASCII.GetBytes(password));

            complete_hash = hasher.ComputeHash(
                Encoding.ASCII.GetBytes(String.Format(
                    "{0}{1}{2}{3}",
                    ByteArrayToHexString(payload_hash),
                    challenge,
                    username,
                    ByteArrayToHexString(password_hash)
                ))
            );

            packet.auth.challenge = challenge;
            packet.auth.chash = ByteArrayToHexString(complete_hash);
            packet.auth.payload = true;
            packet.payload = payload;

            return JsonConvert.SerializeObject(packet);
        }
    }
}

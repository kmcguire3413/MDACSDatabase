using MDACS.API.Requests;
using MDACS.API.Responses;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
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

            byte[] payload_bytes;

            if (auth_url != null)
            {
                payload_bytes = Encoding.UTF8.GetBytes(
                    await Auth.BuildAuthWithPayloadAsync(auth_url, username, password, payload)
                );
            }
            else
            {
                payload_bytes = Encoding.UTF8.GetBytes(payload);
            }

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

            byte[] payload_bytes;

            if (auth_url != null)
            {
                var tsk = Auth.BuildAuthWithPayloadAsync(auth_url, username, password, payload);

                tsk.Wait();

                payload_bytes = Encoding.UTF8.GetBytes(
                    tsk.Result
                );
            } else
            {
                payload_bytes = Encoding.UTF8.GetBytes(payload);
            }

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

        public static async Task<Responses.CommitConfigurationResponse> CommitConfiguration(
            string auth_url,
            string db_url,
            string username,
            string password,
            string deviceid,
            string userid,
            string config_data)
        {
            var req = new Requests.CommitConfigurationRequest()
            {
                deviceid = deviceid,
                config_data = config_data,
                userid = userid,
            };

            var stream = await ReadStreamTransactionAsync(
                auth_url,
                $"{db_url}/commit-configuration",
                username,
                password,
                JsonConvert.SerializeObject(req)
            );

            var bs = new StreamReader(stream, Encoding.UTF8, false);

            return JsonConvert.DeserializeObject<Responses.CommitConfigurationResponse>(await bs.ReadToEndAsync());
        }

        public static async Task<Responses.DeviceConfigResponse> DeviceConfig(
            string auth_url,
            string db_url,
            string username,
            string password,
            string deviceid,
            string current_config_data)
        {
            var stream = await ReadStreamTransactionAsync(
                null,
                $"{db_url}/device-config",
                username,
                password,
                JsonConvert.SerializeObject(new DeviceConfigRequest()
                {
                    deviceid = deviceid,
                    current_config_data = current_config_data,
                })
            );

            var bs = new StreamReader(stream, Encoding.UTF8, false);

            return JsonConvert.DeserializeObject<Responses.DeviceConfigResponse>(await bs.ReadToEndAsync());
        }

        public static async Task<Responses.EnumerateConfigurationsResponse> EnumerateConfigurations(
            string auth_url,
            string db_url,
            string username,
            string password)
        {
            var stream = await ReadStreamTransactionAsync(
                auth_url,
                $"{db_url}/enumerate-configurations",
                username,
                password,
                ""
            );

            var bs = new StreamReader(stream, Encoding.UTF8, false);

            return JsonConvert.DeserializeObject<Responses.EnumerateConfigurationsResponse>(await bs.ReadToEndAsync());
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

        public static async Task<DataResponse> GetDataAsync(string auth_url, string db_url, string username, string password, GetDataProgress progress_event)
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

        public static async Task<bool> DeleteAsync(string auth_url, string db_url, string username, string password, string sid)
        {
            string payload = JsonConvert.SerializeObject(new DeleteRequest()
            {
                sid = sid,
            });

            var stream = await ReadStreamTransactionAsync(
                auth_url,
                string.Format("{0}/data", db_url),
                username,
                password,
                payload
            );

            return JsonConvert.DeserializeObject<DeleteResponse>(new StreamReader(stream).ReadToEnd()).success;
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
                if (progress_event != null)
                {
                    progress_event?.Invoke(amount_read);
                }
            }

            string resp_json = Encoding.UTF8.GetString(ms.ToArray());

            //string resp_json = File.ReadAllText("dump.txt");

            return JsonConvert.DeserializeObject<Responses.DataResponse>(resp_json);
        }
    }
}

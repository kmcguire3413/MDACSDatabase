using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace MDACS.API.Requests
{
    public class DeleteRequest
    {
        public String sid;
    }

    public class AuthUserSetRequest
    {
        public Auth.User user;
    }

    public class AuthUserDeleteRequest
    {
        public string username;
    }

    public class AuthVerifyPayloadRequest
    {
        /// <summary>
        /// The payload hash.
        /// </summary>
        public string phash;
        /// <summary>
        /// The client hash or user's hash.
        /// </summary>
        public string chash;
        /// <summary>
        /// The challenge.
        /// </summary>
        public string challenge;
    }

    public class AuthVerifyRequest
    {
        /// <summary>
        /// The challenge.
        /// </summary>
        public string challenge;
        /// <summary>
        /// The client hash or user's hash.
        /// </summary>
        public string hash;
    }

    public class DeviceConfigRequest
    {
        public String deviceid;
        public String current_config_data;
    }

    public class CommitConfigurationRequest
    {
        public string deviceid;
        public string userid;
        public string config_data;
    }

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

using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace MDACS.API.Requests
{
    public class CommitConfigurationRequest
    {
        public string deviceid;
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

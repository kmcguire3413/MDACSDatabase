using System;
using System.Collections.Generic;
using System.Text;

namespace MDACS.API.Responses
{
    public class VersionResponse
    {
        public int major;
        public int minor;
        public int build;
        public int revision;
    }

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

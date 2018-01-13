using System;
using System.Collections.Generic;
using System.Text;

namespace MDACS.API.Responses
{
    public class DeleteResponse
    {
        public bool success;
    }

    public class AuthLoginValidResponse
    {
        public bool success;
        public Auth.User user;
    }

    public class AuthTokenResponse
    {
        public AuthToken token;
    }

    public class AuthToken
    {
        /// <summary>
        /// A JSON string.
        /// </summary>
        public string data;
        /// <summary>
        /// Base64 encoded bytes representing a signature for validation.
        /// </summary>
        public string signature;
    }

    public class AuthTokenBasedRequest
    {
        /// <summary>
        /// This data type varies with the intended route or service used.
        /// </summary>
        public string data;
        /// <summary>
        /// This follows the convention specified for AuthTokenResponse and is used
        /// to validate, verify, and authenticate using the token the data above.
        /// </summary>
        public AuthToken token;
    }

    public class AuthChallengeResponse
    {
        public string challenge;
    }

    public class DeviceConfigResponse
    {
        public bool success;
        public String config_data;
    }

    public class EnumerateConfigurationsResponse
    {
        public bool success;
        public Dictionary<String, String> configs;
    }

    public class CommitConfigurationResponse
    {
        public bool success;
    }

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

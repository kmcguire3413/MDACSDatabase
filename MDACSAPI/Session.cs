using MDACS.API;
using MDACS.API.Requests;
using MDACS.API.Responses;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace MDACS.API
{
    public class Session
    {
        public string auth_url { get; }
        public string db_url { get; }
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

        public async Task<CommitSetResponse> CommitSetAsync(string sid, JObject meta)
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

        public async Task<HandleBatchSingleOpsResponse> BatchSingleOps(BatchSingleOp[] ops)
        {
            return await Database.BatchSingleOps(
                auth_url,
                db_url,
                username,
                password,
                ops
            );
        }

        public async Task<EnumerateConfigurationsResponse> EnumerateConfigurations()
        {
            return await Database.EnumerateConfigurations(
                auth_url,
                db_url,
                username,
                password
            );
        }

        public async Task<DeviceConfigResponse> DeviceConfig(string deviceid, string current_config_data)
        {
            return await Database.DeviceConfig(
                auth_url,
                db_url,
                username,
                password,
                deviceid,
                current_config_data
            );
        }

        public async Task<DataResponse> Data()
        {
            return await Database.GetDataAsync(
                auth_url,
                db_url,
                username,
                password,
                null
            );
        }

        public async Task<CommitConfigurationResponse> CommitConfigurationAsync(
            string deviceid,
            string userid,
            string config_data
        )
        {
            return await Database.CommitConfiguration(
                auth_url,
                db_url,
                username,
                password,
                deviceid,
                userid,
                config_data
            );
        }

        public async Task<UploadResponse> UploadAsync(
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
}

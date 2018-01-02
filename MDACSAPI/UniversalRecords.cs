using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MDACS.API
{
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
                }
                finally
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
}

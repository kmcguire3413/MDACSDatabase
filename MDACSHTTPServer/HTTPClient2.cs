using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace MDACS.Server
{
    public class HTTPClient2 : HTTPClient
    {
        public enum HTTPRequestMethod
        {
            GET,
            POST,
            UNKNOWN,
        }

        public struct HTTPRequest
        {
            public String url;
            public String url_absolute;
            public Dictionary<String, String> query;
            public HTTPRequestMethod method;
            public Dictionary<String, String> internal_headers;
        }

        public HTTPClient2(IHTTPServerHandler shandler, HTTPDecoder decoder, HTTPEncoder encoder) : base(shandler, decoder, encoder)
        {
        }

        public virtual async Task HandleRequest2(HTTPRequest request, Stream body, ProxyHTTPEncoder encoder)
        {
            throw new Exception("Not Implemented");
        }

        public override async Task HandleRequest(Dictionary<String, String> header, Stream body, ProxyHTTPEncoder encoder)
        {
            var outheader = new Dictionary<String, String>();

            // Ensure the URL parameter actually exists instead of crashing.
            if (!header.ContainsKey("$url"))
            {
                outheader.Add("$response_code", "500");
                outheader.Add("$response_text", "ERROR");
                await encoder.WriteHeader(outheader);
                await encoder.BodyWriteSingleChunk("The request did not specify a URL.");
                return;
            }

            var url = header["$url"];
            var url_absolute = url;
            var query_string = new Dictionary<String, String>();

            // Break down any query string into its key and value parts.
            if (url.IndexOf("?") > -1)
            {
                var qsndx = url.IndexOf("?");
                var qstring = url.Substring(qsndx + 1);
                var qstring_parts = qstring.Split('&');

                foreach (var part in qstring_parts)
                {
                    var eqndx = part.IndexOf("=");
                    query_string.Add(part.Substring(0, eqndx), part.Substring(eqndx + 1));
                }

                url_absolute = url.Substring(0, qsndx);
            }

            HTTPRequest request;

            switch (header["$method"].ToLower()) {
                case "get":
                    request.method = HTTPRequestMethod.GET;
                    break;
                case "post":
                    request.method = HTTPRequestMethod.POST;
                    break;
                default:
                    request.method = HTTPRequestMethod.UNKNOWN;
                    break;
            }

            // Package everything up nice. Hide away the messy implementation details.
            request.internal_headers = header;
            request.query = query_string;
            request.url = url;
            request.url_absolute = url_absolute;

            await HandleRequest2(request, body, encoder);
        }
    }
}

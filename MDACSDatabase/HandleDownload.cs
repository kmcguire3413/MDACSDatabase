using MDACS.Server;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using static MDACS.API.Database;
using static MDACS.Server.HTTPClient2;

namespace MDACS.Database
{
    static class HandleDownload
    {
        public class HandleDownloadRequest
        {
            public String security_id;
        }

        static public async Task Action(ServerHandler shandler, HTTPRequest request, Stream body, ProxyHTTPEncoder encoder)
        {
            // This URL route was never implemented with user security. The security ID itself is the security, but that
            // could be revised.
            //var auth = await ReadMessageFromStreamAndAuthenticate(1024 * 16, body);
            //if (!auth.success)
            //{
            //    throw new UnauthorizedException();
            //}

            HandleDownloadRequest req = null;

            String download_sid;

            if (request.query_string.Length > 0)
            {
                download_sid = request.query_string;
            }
            else
            {
                //req = JsonConvert.DeserializeObject<HandleDownloadRequest>(auth.payload);
                //download_sid = req.security_id;
                throw new InvalidArgumentException();
            }

            Item item;

            lock (shandler.items)
            {
                if (!shandler.items.ContainsKey(download_sid))
                {
                    throw new InvalidArgumentException();
                }

                item = shandler.items[download_sid];
            }

            var item_data_path = Path.Combine(shandler.data_path, item.node);

            var fd = File.OpenRead(item_data_path);

            ulong offset_start = 0;
            // How can a stream be negative? Could this stream ever be negative? What purpose does it suit?
            ulong offset_size = (ulong)fd.Length;
            ulong total_size = (ulong)fd.Length;

            String response_code = "200";

            if (request.internal_headers.ContainsKey("range"))
            {
                var range_str = request.internal_headers["range"];

                var eqndx = range_str.IndexOf("=");

                if (eqndx > -1)
                {
                    var range_sub_str = range_str.Substring(eqndx + 1).Trim();
                    var range_nums_strs = range_sub_str.Split("-");

                    if (range_nums_strs.Length > 1)
                    {
                        offset_start = ulong.Parse(range_nums_strs[0]);

                        if (range_nums_strs[1].Equals(""))
                        {
                            offset_size = (ulong)fd.Length - offset_start;
                        }
                        else
                        {
                            offset_size = ulong.Parse(range_nums_strs[1]) - offset_start + 1;
                        }
                        response_code = "206";
                    }
                }
            }

            checked
            {
                fd.Seek((long)offset_start, SeekOrigin.Begin);
            }

            fd.Seek((long)offset_start, SeekOrigin.Begin);

            String mime_type = null;

            switch (item.datatype)
            {
                case "mp4":
                    mime_type = "video/mp4";
                    break;
                case "jpg":
                    mime_type = "image/jpeg";
                    break;
            }

            using (var de_stream = new LimitedStream(fd, offset_size))
            {
                var header = new Dictionary<String, String>();

                header.Add("$response_code", response_code);
                header.Add("$response_text", "Partial Content");
                header.Add("content-disposition",
                    String.Format("inline; filename=\"{0}_{1}_{2}_{3}.{4}\"",
                        item.datestr,
                        item.userstr,
                        item.devicestr,
                        item.timestr,
                        item.datatype
                    )
                );
                header.Add("accept-ranges", "bytes");
                header.Add("content-range",
                    String.Format("bytes {0}-{1}/{2}",
                        offset_start,
                        offset_size + offset_start - 1,
                        total_size
                    )
                );

                if (mime_type != null)
                {
                    header.Add("content-type", mime_type);
                }

                await encoder.WriteHeader(header);
                await encoder.BodyWriteStream(de_stream);
            }
        }
    }
}

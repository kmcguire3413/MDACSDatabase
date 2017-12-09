using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace MDACS.Server
{
    enum HTTPEncoderState
    {
        SendingHeaders,
        SendingChunkedBody,
        SendingContentLengthBody,
    }

    class HTTPEncoder
    {
        private Stream s;
        private HTTPEncoderState state;
        private Dictionary<String, String> header;

        public HTTPEncoder(Stream s)
        {
            this.s = s;
        }

        public async Task WriteHeader(Dictionary<String, String> header)
        {
            this.header = header;
        }

        public async Task DoHeaders() {
            String response_code = "500";
            String response_text = "ERROR";

            if (header.ContainsKey("$response_code"))
            {

                if (!header.TryGetValue("$response_code", out response_code))
                {
                    response_code = "500";
                }

                if (!header.TryGetValue("$response_text", out response_text))
                {
                    response_text = "ERROR";
                }
            }

            Console.WriteLine("Sent HTTP/1.1 status");
            String _line = String.Format("HTTP/1.1 {0} {1}\r\n", response_code, response_text);
            byte[] _line_bytes = Encoding.UTF8.GetBytes(_line);
            Console.WriteLine("@@@method");
            await s.WriteAsync(_line_bytes, 0, _line_bytes.Length);

            foreach (var pair in header)
            {
                if (pair.Key[0] == '$')
                {
                    continue;
                }

                Console.WriteLine(String.Format("Sent line; {0}={1}", pair.Key, pair.Value));

                String line = String.Format("{0}: {1}\r\n", pair.Key, pair.Value);

                byte[] line_bytes = Encoding.UTF8.GetBytes(line);

                Console.WriteLine("@@@header");
                await s.WriteAsync(line_bytes, 0, line_bytes.Length);
            }
        }

        public async Task BodyWriteSingleChunk(byte[] chunk, int offset, int length)
        {
            if (header == null)
            {
                throw new Exception("Headers must be set first.");
            }

            if (header.ContainsKey("content-length"))
            {
                header["content-length"] = length.ToString();
            } else
            {
                header.Add("content-length", length.ToString());
            }

            header["content-encoding"] = "ASCII";

            await DoHeaders();

            byte[] tmp = new byte[2];
            tmp[0] = (byte)'\r';
            tmp[0] = (byte)'\n';

            await s.WriteAsync(tmp, 0, tmp.Length);

            await s.WriteAsync(chunk, offset, length);

            await s.WriteAsync(tmp, 0, tmp.Length);
        }

        public async Task BodyWriteFirstChunk(byte[] buf, int offset, int length)
        {
            if (header == null)
            {
                throw new Exception("Headers must be set first.");
            }

            if (header.ContainsKey("transfer-encoding"))
            {
                header["Transfer-Encoding"] = "chunked";
            }
            else
            {
                header.Add("Transfer-Encoding", "chunked");
            }

            await DoHeaders();

            byte[] tmp = new byte[2];
            tmp[0] = (byte)'\r';
            tmp[1] = (byte)'\n';

            await s.WriteAsync(tmp, 0, tmp.Length);

            byte[] chunk_header = Encoding.UTF8.GetBytes(String.Format("{0:X}\r\n", length));

            await s.WriteAsync(chunk_header, 0, chunk_header.Length);
            await s.WriteAsync(buf, offset, length);

            await s.WriteAsync(tmp, 0, tmp.Length);
        }

        public async Task BodyWriteNextChunk(byte[] buf, int offset, int length)
        {
            byte[] chunk_header = Encoding.UTF8.GetBytes(String.Format("{0:X}r\n", length));

            await s.WriteAsync(chunk_header, 0, chunk_header.Length);
            await s.WriteAsync(buf, offset, length);
            byte[] tmp = new byte[2];
            tmp[0] = (byte)'\r';
            tmp[1] = (byte)'\n';
            await s.WriteAsync(tmp, 0, tmp.Length);
        }

        public async Task BodyWriteNoChunk()
        {
            byte[] chunk_header = Encoding.UTF8.GetBytes("0\r\n\r\n");
            await s.WriteAsync(chunk_header, 0, chunk_header.Length);
        }

    }
}

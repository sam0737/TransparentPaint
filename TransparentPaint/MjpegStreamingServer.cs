using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Hellosam.Net.TransparentPaint
{
    class MjpegStreamingServer : StreamingServer
    {
        public static readonly string BOUNDARY = "--B-" + Guid.NewGuid().ToString();

        protected override async Task<bool> HandleRequest(Stream ns, HttpRequest request)
        {
            if (request.DecodedUri != null && request.DecodedUri.LocalPath == "/html")
            {
                var content =
@"<!DOCTYPE html>
<head><meta charset=""UTF-8""></head>
<body><img src=""/"" style=""width: 100%; height: 100%""></body></html>
";
                var contentBytes = Encoding.UTF8.GetBytes(content);
                var header =
                    "HTTP/1.1 200 OK\r\n" +
                    "Cache-Control: no-cache\r\n" +
                    "Content-Length: " + contentBytes.Length.ToString(CultureInfo.InvariantCulture) + "\r\n" +
                    "Content-Type: text/html\r\n\r\n";

                var headerBytes = Encoding.ASCII.GetBytes(header);

                using (var timeout = new CancellationTokenSource(3000))
                {
                    timeout.Token.Register(() => ns.Close());
                    await ns.WriteAsync(headerBytes, 0, headerBytes.Length);
                    await ns.WriteAsync(contentBytes, 0, contentBytes.Length);
                }
                return true;
            }
            return false;
        }

        protected override async Task WriteStreamingHeader(Stream ns)
        {
            var b = Encoding.ASCII.GetBytes(
                    "HTTP/1.1 200 OK\r\n" +
                    "Content-Type: multipart/x-mixed-replace; boundary=" +
                    BOUNDARY +
                    "\r\n"
                 );
            await ns.WriteAsync(b, 0, b.Length);
            await ns.FlushAsync();
        }
    }

    class MjpegPayload : StreamingPayload
    {
        Image _image;
        public MjpegPayload(Image image)
        {
            _image = image;
        }

        public override async Task WriteToStream(Stream ns)
        {
            using (var buf = new MemoryStream())
            {
                _image.Save(buf, System.Drawing.Imaging.ImageFormat.Jpeg);

                var header =
                    "\r\n" +
                    MjpegStreamingServer.BOUNDARY + "\r\n" +
                    "Content-Type: image/jpeg\r\n" +
                    "Content-Length: " + buf.Length.ToString(CultureInfo.InvariantCulture) +
                    "\r\n\r\n";
                var headerBytes = Encoding.ASCII.GetBytes(header);
                await ns.WriteAsync(headerBytes, 0, headerBytes.Length);
                await buf.CopyToAsync(ns);
            }
        }
    }

    class BinaryPayload : StreamingPayload
    {
        string _mime;
        byte[] _payload;
        public BinaryPayload(string mime, byte[] payload)
        {
            _mime = mime;
            _payload = payload;
        }

        public override async Task WriteToStream(Stream ms)
        {
            using (var buf = new MemoryStream())
            {
                var t =
                    "\r\n" +
                    MjpegStreamingServer.BOUNDARY + "\r\n" +
                    "Content-Type: image/jpeg\r\n" +
                    "Content-Length: " + _payload.Length.ToString(CultureInfo.InvariantCulture) +
                    "\r\n\r\n";
                var tb = Encoding.ASCII.GetBytes(t);
                await ms.WriteAsync(tb, 0, tb.Length);
                await ms.WriteAsync(_payload, 0, _payload.Length);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hellosam.Net.TransparentPaint
{
    class MjpegStreamingServer : StreamingServer
    {
        public static readonly string BOUNDARY = "--B-" + Guid.NewGuid().ToString();

        protected override async Task WriteHeader(Stream ms)
        {
            var b = Encoding.ASCII.GetBytes(
                    "HTTP/1.1 200 OK\r\n" +
                    "Content-Type: multipart/x-mixed-replace; boundary=" +
                    BOUNDARY +
                    "\r\n"
                 );
            await ms.WriteAsync(b, 0, b.Length);
            await ms.FlushAsync();
        }
    }

    class MjpegPayload : StreamingPayload
    {
        Image _image;
        public MjpegPayload(Image image)
        {
            _image = image;
        }

        public override async Task WriteToStream(Stream ms)
        {
            using (var buf = new MemoryStream())
            {
                _image.Save(buf, System.Drawing.Imaging.ImageFormat.Jpeg);

                var t =
                    "\r\n" +
                    MjpegStreamingServer.BOUNDARY + "\r\n" +
                    "Content-Type: image/jpeg\r\n" +
                    "Content-Length: " + buf.Length.ToString(CultureInfo.InvariantCulture) +
                    "\r\n\r\n";
                var tb = Encoding.ASCII.GetBytes(t);
                await ms.WriteAsync(tb, 0, tb.Length);
                await buf.CopyToAsync(ms);
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

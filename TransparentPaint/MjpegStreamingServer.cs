using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Hellosam.Net.TransparentPaint
{
    class MjpegStreamingServer : StreamingServer
    {
        protected override StreamingServerWorker CreateWorker(Socket client)
        {
            return new MjpegStreamingServerWorker(this, client, Cts.Token);
        }

        public bool AnyClientWithTag(string tag)
        {
            return ClientTasks.Any(c => ((MjpegStreamingServerWorker)c.Value).Tag == tag);
        }
    }

    class MjpegStreamingServerWorker : StreamingServerWorker
    {
        public string Tag { get; private set; }

        public MjpegStreamingServerWorker(StreamingServer server, Socket client, CancellationToken ct)
            :base(server, client, ct)
        {
        }

        public static readonly string BOUNDARY = "--B-" + Guid.NewGuid().ToString();

        protected override async Task<bool> HandleRequest(HttpRequest request)
        {
            if (request.DecodedUri == null)
            {
                await SendTextRensponse("404 Not Found", "text/plain", "Not Found");
                return true;
            }

            if (request.DecodedUri.LocalPath.StartsWith("/img/", StringComparison.Ordinal) ||
                request.DecodedUri.LocalPath == "/img")
            {
                Tag = new Regex("[^a-zA-Z0-9_]").Replace(request.DecodedUri.LocalPath, string.Empty);
                return false;
            }

            if (request.DecodedUri.LocalPath.StartsWith("/ping/", StringComparison.Ordinal))
            {
                var tag = request.DecodedUri.LocalPath.Substring("/ping/".Length);
                if (((MjpegStreamingServer)Server).AnyClientWithTag(tag))
                {
                    await SendOkTextRensponse("text/plain", "live");
                } else
                {
                    await SendOkTextRensponse("text/plain", "dead");
                }
                return false;
            }

            if (request.DecodedUri.LocalPath == "/")
            {
                using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Hellosam.Net.TransparentPaint.landing.html"))
                using (var sr = new StreamReader(s, Encoding.UTF8, false))
                {
                    await SendOkTextRensponse("text/html", sr.ReadToEnd());
                }
                return true;
            }
            await SendTextRensponse("404 Not Found", "text/plain", "Not Found");
            return true;
        }
        
        protected override async Task WriteStreamingHeader()
        {
            using (var timeout = new CancellationTokenSource(3000))
            {
                var b = Encoding.ASCII.GetBytes(
                    "HTTP/1.1 200 OK\r\n" +
                    "Content-Type: multipart/x-mixed-replace; boundary=" +
                    BOUNDARY +
                    "\r\n"
                 );
                await Ns.WriteAsync(b, 0, b.Length);
                await Ns.FlushAsync();
            }
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
                    MjpegStreamingServerWorker.BOUNDARY + "\r\n" +
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
            var t =
                "\r\n" +
                MjpegStreamingServerWorker.BOUNDARY + "\r\n" +
                "Content-Type: " + _mime + "\r\n" +
                "Content-Length: " + _payload.Length.ToString(CultureInfo.InvariantCulture) +
                "\r\n\r\n";
            var tb = Encoding.ASCII.GetBytes(t);
            await ms.WriteAsync(tb, 0, tb.Length);
            await ms.WriteAsync(_payload, 0, _payload.Length);
        }
    }
}

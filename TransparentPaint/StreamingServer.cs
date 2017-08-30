using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Windows.Forms;

namespace Hellosam.Net.TransparentPaint
{
    abstract class StreamingPayload
    {
        public abstract Task WriteToStream(Stream ns);
    }

    abstract class StreamingServer
    {
        private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(StreamingServer));

        protected CancellationTokenSource Cts { get; private set; }
        public BroadcastBlock<StreamingPayload> RootBuffer { get; private set; }
        protected ConcurrentDictionary<Task, StreamingServerWorker> ClientTasks { get; private set; }

        private Task _serverTask;

        public StreamingServer()
        {
            RootBuffer = new BroadcastBlock<StreamingPayload>(x => x);
            Cts = new CancellationTokenSource();
            ClientTasks = new ConcurrentDictionary<Task, StreamingServerWorker>();
        }

        public Task Start(int port)
        {
            if (_serverTask != null)
                throw new InvalidOperationException("Server was started before. Please create a new instance.");

            return _serverTask = Task.Run(() => ServerThread(port, Cts.Token));
        }

        public async Task Stop()
        {
            Cts.Cancel();
            await _serverTask;
        }

        public void Publish(StreamingPayload payload)
        {
            RootBuffer.Post(payload);
        }

        abstract protected StreamingServerWorker CreateWorker(Socket client);

        private async Task ServerThread(int port, CancellationToken ct)
        {
            Task[] serverTask = { Task.Delay(-1, ct), null };

            using (Socket server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                server.Bind(new IPEndPoint(IPAddress.Any, port));
                server.Listen(10);

                Logger.InfoFormat("Server started on port {0}.", port);

                while (true)
                {
                    if (serverTask[1] == null)
                        serverTask[1] = Task.Factory.FromAsync<Socket>(server.BeginAccept, server.EndAccept, null);
                    var task = await Task.WhenAny(serverTask.Concat(ClientTasks.Keys));
                    if (task == serverTask[1])
                    {
                        Socket client = await (Task<Socket>)serverTask[1];
                        serverTask[1] = null;

                        StreamingServerWorker worker = CreateWorker(client);
                        var t = Task.Run(() => worker.ClientThread());
                        ClientTasks[t] = worker;
                    }
                    else if (task == serverTask[0])
                    {
                        try { await task; } catch (TaskCanceledException) { }
                        break;
                    }
                    else
                    {
                        // Clean up finished clients
                        await task;
                        StreamingServerWorker dummy;
                        ClientTasks.TryRemove(task, out dummy);
                    }
                }
            }

            // Clean up
            Logger.Debug("HTTP server cleaning up");

            Cts.Cancel(); // To be safe

            if (serverTask[1] != null)
                try { await serverTask[1]; }
                catch (ObjectDisposedException) { }

            await Task.WhenAll(ClientTasks.Keys);
            foreach (var t in ClientTasks.Keys)
            {
                try { await t; }
                catch (TaskCanceledException) { }
            }
        }
    }

    abstract class StreamingServerWorker
    {
        protected Socket Client { get; private set; }
        protected NetworkStream Ns { get; private set; }
        protected StreamingServer Server { get; private set; }
        protected CancellationToken Ct { get; private set; }

        public StreamingServerWorker(StreamingServer server, Socket client, CancellationToken ct)
        {
            Client = client;
            Server = server;
            Ct = ct;
            Ct.Register(() => client.Close());
        }

        abstract protected Task WriteStreamingHeader();

        abstract protected Task<bool> HandleRequest(HttpRequest request);
        
        protected async Task SendOkTextRensponse(string mimeType, string content)
        {
            await SendTextRensponse("200 OK", mimeType, content);
        }

        protected async Task SendTextRensponse(string status, string mimeType, string content)
        {
            var contentBytes = Encoding.UTF8.GetBytes(content);
            var header =
                "HTTP/1.1 " + status + "\r\n" +
                "Cache-Control: no-cache\r\n" +
                "Content-Length: " + contentBytes.Length.ToString(CultureInfo.InvariantCulture) + "\r\n" +
                "Content-Type: " + mimeType + "\r\n\r\n";

            var headerBytes = Encoding.ASCII.GetBytes(header);

            using (var timeout = new CancellationTokenSource(3000))
            {
                timeout.Token.Register(() => Ns.Close());
                await Ns.WriteAsync(headerBytes, 0, headerBytes.Length);
                await Ns.WriteAsync(contentBytes, 0, contentBytes.Length);
                await Ns.FlushAsync();
            }
        }

        public async Task ClientThread()
        {
            try
            {
                var bufferBlock = new BufferBlock<StreamingPayload>(new DataflowBlockOptions { BoundedCapacity = 1 });
                using (Server.RootBuffer.LinkTo(bufferBlock))
                using (Ns = new NetworkStream(Client, true))
                {
                    while (true)
                    {
                        // Process Request
                        var httpRequest = await ReadRequsetLine();
                        if (httpRequest == null)
                            return;

                        // If it's handled by implementation, re-read the request again
                        if (await HandleRequest(httpRequest))
                            continue;
                        break;
                    }

                    // All unhandled requests are assumed to be the image streaming request
                    await WriteStreamingHeader();

                    StreamingPayload payload;
                    // First frame
                    if (Server.RootBuffer.TryReceive(out payload))
                    {
                        await payload.WriteToStream(Ns);
                        await Ns.FlushAsync();
                    }

                    while (true)
                    {
                        payload = await bufferBlock.ReceiveAsync(Ct);
                        await payload.WriteToStream(Ns);
                        await Ns.FlushAsync();
                    }
                }
            }
            catch (IOException)
            {
                // The client might went away at anytime
                return;
            }
            catch (ObjectDisposedException)
            {
                // The client might went away at anytime
                return;
            }
        }

        static readonly Regex requestLineMatcher = new Regex("^([^ ]+) +([^ ]+) +");
        private async Task<HttpRequest> ReadRequsetLine()
        {
            using (var timeout = new CancellationTokenSource(3000))
            {
                timeout.Token.Register(() => Ns.Close());

                string requestLine = null;
                using (var sr = new StreamReader(Ns, Encoding.ASCII, false, 65535, true))
                {
                    requestLine = await sr.ReadLineAsync();
                    if (string.IsNullOrEmpty(requestLine))
                        return null;

                    // Discard all other headers
                    while (true)
                    {
                        var dummy = await sr.ReadLineAsync();
                        if (string.IsNullOrEmpty(dummy))
                            break;
                    }
                }

                if (requestLine != null)
                {
                    var m = requestLineMatcher.Match(requestLine);
                    if (m.Success)
                    {
                        var result = new HttpRequest { Method = m.Groups[1].Value, RawUri = m.Groups[2].Value };
                        try
                        {
                            result.DecodedUri = new Uri(new Uri("http://localhost/"), result.RawUri);
                        }
                        catch (FormatException)
                        {
                            // The Uri may be invalid
                        }
                        return result;
                    }
                }
            }
            return null;
        }
    }
    class HttpRequest
    {
        public string Method;
        public string RawUri;
        public Uri DecodedUri;
    }
}

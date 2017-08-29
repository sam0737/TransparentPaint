using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Windows.Forms;

namespace Hellosam.Net.TransparentPaint
{
    abstract class StreamingPayload
    {
        public abstract Task WriteToStream(Stream ms);
    }

    abstract class StreamingServer
    {
        private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(StreamingServer));

        CancellationTokenSource _cts;
        BroadcastBlock<StreamingPayload> _rootBuffer = new BroadcastBlock<StreamingPayload>(x => x);
        private Task _serverTask;

        public StreamingServer()
        {

        }

        public Task Start(int port)
        {
            var cts = _cts = new CancellationTokenSource();
            return _serverTask = Task.Run(() => ServerThread(port, cts.Token));
        }

        public async Task Stop()
        {
            _cts.Cancel();
            await _serverTask;
        }

        public void Publish(StreamingPayload payload)
        {
            _rootBuffer.Post(payload);
        }

        private async Task ServerThread(int port, CancellationToken ct)
        {
            List<Task> clientTasks = new List<Task>();
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
                    var task = await Task.WhenAny(serverTask.Concat(clientTasks));
                    if (task == serverTask[1])
                    {
                        Socket client;
                            client = await (Task<Socket>)serverTask[1];
                        serverTask[1] = null;
                        var t = Task.Run(() => ClientThread(client, ct));
                        clientTasks.Add(t);
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
                        clientTasks.Remove(task);
                    }
                }
            }

            // Clean up
            Logger.Debug("HTTP server cleaning up");

            _cts.Cancel(); // To be safe

            if (serverTask[1] != null)
                try { await serverTask[1]; }
                catch (ObjectDisposedException) { }
            
            await Task.WhenAll(clientTasks);
            foreach (var t in clientTasks)
            {
                try { await t; }
                catch (TaskCanceledException) { }
            }
        }

        abstract protected Task WriteHeader(Stream ms);

        private async Task ClientThread(Socket client, CancellationToken ct)
        {
            var bufferBlock = new BufferBlock<StreamingPayload>(new DataflowBlockOptions { BoundedCapacity = 1 });
            using (_rootBuffer.LinkTo(bufferBlock))
            using (var ns = new NetworkStream(client, true))
            {
                try
                {
                    await WriteHeader(ns);
                    StreamingPayload payload;
                    // First frame
                    if (_rootBuffer.TryReceive(out payload))
                    {
                        await payload.WriteToStream(ns);
                        await ns.FlushAsync();
                    }

                    while (true)
                    {
                        payload = await bufferBlock.ReceiveAsync(ct);
                        await payload.WriteToStream(ns);
                        await ns.FlushAsync();
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
        }        
    }
}

using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.Sockets.Kcp;
using System.Net.WebSockets;
using System.Text;
using ULinkRPC.Core;
using ULinkRPC.Server;
using ULinkRPC.Transport.Kcp;
using ULinkRPC.Transport.WebSocket;
using Xunit;

namespace RpcCall.MemoryPack.Server.Tests;

public class TransportModeTests
{
    [Fact]
    public async Task WebSocketTransport_Roundtrip()
    {
        var port = GetFreePort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/rpc/");
        listener.Start();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var serverTask = Task.Run(async () =>
        {
            var ctx = await WithTimeout(listener.GetContextAsync(), cts.Token);
            var wsContext = await WithTimeout(ctx.AcceptWebSocketAsync(null), cts.Token);
            await using var transport = new WebSocketServerTransport(wsContext.WebSocket);

            var payload = await WithTimeout(transport.ReceiveFrameAsync(cts.Token), cts.Token);
            Assert.Equal("ping-ws", Encoding.UTF8.GetString(payload.Span));

            await WithTimeout(
                transport.SendFrameAsync(Encoding.UTF8.GetBytes("pong-ws"), cts.Token),
                cts.Token);
        }, cts.Token);

        try
        {
            using var client = new ClientWebSocket();
            await WithTimeout(
                client.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/rpc/"), cts.Token),
                cts.Token);

            await WithTimeout(
                client.SendAsync(
                    LengthPrefix.Pack(Encoding.UTF8.GetBytes("ping-ws")),
                    WebSocketMessageType.Binary,
                    true,
                    cts.Token),
                cts.Token);

            var response = await WithTimeout(ReceiveWebSocketFrameAsync(client, cts.Token), cts.Token);
            Assert.Equal("pong-ws", Encoding.UTF8.GetString(response.Span));

            try
            {
                if (client.State == WebSocketState.Open || client.State == WebSocketState.CloseReceived)
                    await WithTimeout(
                        client.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", cts.Token),
                        cts.Token);
            }
            catch
            {
            }

            await WithTimeout(serverTask, cts.Token);
        }
        finally
        {
            try
            {
                listener.Stop();
            }
            catch
            {
            }

            if (!serverTask.IsCompleted)
                try
                {
                    await WithTimeout(serverTask, cts.Token);
                }
                catch
                {
                }
        }
    }

    [Fact]
    public async Task KcpTransport_Roundtrip()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        using var serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        serverSocket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        var serverEndPoint = (IPEndPoint)serverSocket.LocalEndPoint!;

        using var clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        clientSocket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        var clientEndPoint = (IPEndPoint)clientSocket.LocalEndPoint!;

        const uint conv = 17;
        await using var serverTransport = new KcpServerTransport(
            serverSocket,
            clientEndPoint,
            conv,
            ReadOnlyMemory<byte>.Empty);
        await serverTransport.ConnectAsync(cts.Token);

        await using var client = new KcpTestClient(clientSocket, serverEndPoint, conv);
        await client.ConnectAsync(cts.Token);

        var payload = Encoding.UTF8.GetBytes("ping-kcp");
        await client.SendFrameAsync(payload, cts.Token);
        var serverReceived = await WithTimeout(serverTransport.ReceiveFrameAsync(cts.Token), cts.Token);
        Assert.Equal(payload, serverReceived.ToArray());

        var reply = Encoding.UTF8.GetBytes("pong-kcp");
        await serverTransport.SendFrameAsync(reply, cts.Token);
        var clientReceived = await WithTimeout(client.ReceiveFrameAsync(cts.Token), cts.Token);
        Assert.Equal(reply, clientReceived.ToArray());
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task<ReadOnlyMemory<byte>> ReceiveWebSocketFrameAsync(ClientWebSocket ws, CancellationToken ct)
    {
        var accum = Array.Empty<byte>();

        while (true)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(8 * 1024);
            try
            {
                var res = await ws.ReceiveAsync(buffer, ct);
                if (res.MessageType == WebSocketMessageType.Close)
                    throw new IOException("WebSocket closed.");

                var oldLen = accum.Length;
                Array.Resize(ref accum, oldLen + res.Count);
                Array.Copy(buffer, 0, accum, oldLen, res.Count);

                var seq = new ReadOnlySequence<byte>(accum);
                if (LengthPrefix.TryUnpack(ref seq, out var payloadSeq))
                {
                    var payload = payloadSeq.ToArray();
                    accum = seq.ToArray();
                    return payload;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    private static async Task WithTimeout(Task task, CancellationToken ct)
    {
        var delay = Task.Delay(Timeout.InfiniteTimeSpan, ct);
        var completed = await Task.WhenAny(task, delay);
        if (completed != task)
            throw new TimeoutException("Operation timed out.");

        await task;
    }

    private static async Task<T> WithTimeout<T>(Task<T> task, CancellationToken ct)
    {
        var delay = Task.Delay(Timeout.InfiniteTimeSpan, ct);
        var completed = await Task.WhenAny(task, delay);
        if (completed != task)
            throw new TimeoutException("Operation timed out.");

        return await task;
    }

    private static async ValueTask WithTimeout(ValueTask task, CancellationToken ct)
    {
        await WithTimeout(task.AsTask(), ct);
    }

    private static async ValueTask<T> WithTimeout<T>(ValueTask<T> task, CancellationToken ct)
    {
        return await WithTimeout(task.AsTask(), ct);
    }

    private sealed class KcpTestClient : IAsyncDisposable, IKcpCallback, IRentable
    {
        private const int MaxFrameSize = 64 * 1024 * 1024;
        private const int UpdateIntervalMs = 10;
        private readonly CancellationTokenSource _cts = new();
        private readonly ConcurrentQueue<ReadOnlyMemory<byte>> _frames = new();
        private readonly SimpleSegManager.Kcp _kcp;
        private readonly object _kcpGate = new();
        private readonly EndPoint _remote;
        private readonly Socket _socket;
        private byte[] _accum = Array.Empty<byte>();
        private Task? _updateLoop;

        public KcpTestClient(Socket socket, EndPoint remote, uint conv)
        {
            _socket = socket ?? throw new ArgumentNullException(nameof(socket));
            _remote = remote ?? throw new ArgumentNullException(nameof(remote));
            _kcp = new SimpleSegManager.Kcp(conv, this, this);
        }

        public bool IsConnected { get; private set; }

        public ValueTask ConnectAsync(CancellationToken ct = default)
        {
            if (IsConnected)
                return default;

            IsConnected = true;
            _updateLoop = Task.Run(UpdateLoopAsync);
            return default;
        }

        public ValueTask SendFrameAsync(ReadOnlyMemory<byte> frame, CancellationToken ct = default)
        {
            if (!IsConnected)
                throw new InvalidOperationException("Not connected.");

            var packed = LengthPrefix.Pack(frame.Span);
            lock (_kcpGate)
            {
                _kcp.Send(packed);
                var now = DateTimeOffset.UtcNow;
                _kcp.Update(in now);
            }

            return default;
        }

        public async ValueTask<ReadOnlyMemory<byte>> ReceiveFrameAsync(CancellationToken ct = default)
        {
            if (!IsConnected)
                throw new InvalidOperationException("Not connected.");

            var buffer = new byte[64 * 1024];
            EndPoint any = new IPEndPoint(IPAddress.Any, 0);

            while (true)
            {
                if (TryDequeueFrame(out var queued))
                    return queued;

                SocketReceiveFromResult res;
#if NET8_0_OR_GREATER
                res = await _socket.ReceiveFromAsync(buffer, SocketFlags.None, any, ct);
#else
                res = await _socket.ReceiveFromAsync(new ArraySegment<byte>(buffer), SocketFlags.None, any);
#endif
                if (!EndPointEquals(res.RemoteEndPoint, _remote))
                    continue;

                ProcessInput(buffer.AsSpan(0, res.ReceivedBytes));

                if (TryDequeueFrame(out var frame))
                    return frame;
            }
        }

        public async ValueTask DisposeAsync()
        {
            IsConnected = false;
            _cts.Cancel();

            if (_updateLoop is not null)
                try
                {
                    await _updateLoop;
                }
                catch (OperationCanceledException)
                {
                }

            _cts.Dispose();
            _kcp.Dispose();

            try
            {
                _socket.Dispose();
            }
            catch
            {
            }
        }

        void IKcpCallback.Output(IMemoryOwner<byte> buffer, int avalidLength)
        {
            try
            {
                var mem = buffer.Memory.Slice(0, avalidLength);
#if NET8_0_OR_GREATER
                _socket.SendTo(mem.Span, SocketFlags.None, _remote);
#else
                var tmp = mem.ToArray();
                _socket.SendTo(tmp, 0, tmp.Length, SocketFlags.None, _remote);
#endif
            }
            finally
            {
                buffer.Dispose();
            }
        }

        IMemoryOwner<byte> IRentable.RentBuffer(int size)
        {
            return MemoryPool<byte>.Shared.Rent(size);
        }

        private void ProcessInput(ReadOnlySpan<byte> data)
        {
            lock (_kcpGate)
            {
                _kcp.Input(data);
                DrainKcp();
            }
        }

        private void DrainKcp()
        {
            while (true)
            {
                var size = _kcp.PeekSize();
                if (size <= 0)
                    break;

                if (size > MaxFrameSize)
                    throw new InvalidOperationException($"Frame too large: {size} bytes");

                var buf = new byte[size];
                _kcp.Recv(buf);
                AppendAndUnpack(buf);
            }
        }

        private void AppendAndUnpack(ReadOnlySpan<byte> payload)
        {
            var oldLen = _accum.Length;
            Array.Resize(ref _accum, oldLen + payload.Length);
            payload.CopyTo(_accum.AsSpan(oldLen));

            while (true)
            {
                var seq = new ReadOnlySequence<byte>(_accum);
                if (!LengthPrefix.TryUnpack(ref seq, out var payloadSeq))
                    break;

                var frame = payloadSeq.ToArray();
                if (frame.Length > 0)
                    EnqueueFrame(frame);

                _accum = seq.ToArray();
            }
        }

        private bool TryDequeueFrame(out ReadOnlyMemory<byte> frame)
        {
            return _frames.TryDequeue(out frame);
        }

        private void EnqueueFrame(ReadOnlyMemory<byte> frame)
        {
            _frames.Enqueue(frame);
        }

        private async Task UpdateLoopAsync()
        {
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    lock (_kcpGate)
                    {
                        var now = DateTimeOffset.UtcNow;
                        _kcp.Update(in now);
                    }

                    await Task.Delay(UpdateIntervalMs, _cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private static bool EndPointEquals(EndPoint? a, EndPoint? b)
        {
            return a is not null && b is not null && a.Equals(b);
        }
    }
}


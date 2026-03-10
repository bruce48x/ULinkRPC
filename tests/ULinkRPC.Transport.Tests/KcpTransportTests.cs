using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.Sockets.Kcp;
using System.Text;
using ULinkRPC.Core;
using ULinkRPC.Transport.Kcp;

namespace ULinkRPC.Transport.Tests;

public class KcpTransportTests
{
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

                var res = await _socket.ReceiveFromAsync(buffer, SocketFlags.None, any, ct);
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
                _socket.SendTo(mem.Span, SocketFlags.None, _remote);
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
                    _frames.Enqueue(frame);

                _accum = seq.ToArray();
            }
        }

        private bool TryDequeueFrame(out ReadOnlyMemory<byte> frame)
        {
            return _frames.TryDequeue(out frame);
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

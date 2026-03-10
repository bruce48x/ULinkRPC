using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.Sockets.Kcp;
using ULinkRPC.Core;

namespace ULinkRPC.Transport.Kcp
{
    public sealed class KcpTransport : ITransport, IKcpCallback, IRentable
    {
        private const int MaxFrameSize = 64 * 1024 * 1024;
        private const int UpdateIntervalMs = 10;
        private readonly CancellationTokenSource _cts = new();
        private readonly ConcurrentQueue<ReadOnlyMemory<byte>> _frames = new();
        private readonly object _kcpGate = new();
        private readonly string _host;
        private readonly int _port;
        private byte[] _accum = Array.Empty<byte>();
        private SimpleSegManager.Kcp? _kcp;
        private EndPoint? _remote;
        private Socket? _socket;
        private Task? _updateLoop;

        public KcpTransport(string host, int port)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _port = port;
        }

        public bool IsConnected { get; private set; }

        public async ValueTask ConnectAsync(CancellationToken ct = default)
        {
            if (IsConnected)
                return;

            var ipAddress = await ResolveHostAsync(_host).ConfigureAwait(false);
            var bootstrapEndPoint = new IPEndPoint(ipAddress, _port);
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Bind(new IPEndPoint(IPAddress.Any, 0));

            var conv = CreateConversationId();
            socket.SendTo(CreateHandshakeRequest(conv), bootstrapEndPoint);

            var sessionPort = await ReceiveHandshakeAckAsync(socket, bootstrapEndPoint, conv).ConfigureAwait(false);
            _remote = new IPEndPoint(ipAddress, sessionPort);
            _socket = socket;
            _kcp = new SimpleSegManager.Kcp(conv, this, this);
            IsConnected = true;
            _updateLoop = Task.Run(UpdateLoopAsync);
        }

        public ValueTask SendFrameAsync(ReadOnlyMemory<byte> frame, CancellationToken ct = default)
        {
            if (!IsConnected || _kcp is null)
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
            if (!IsConnected || _socket is null || _remote is null)
                throw new InvalidOperationException("Not connected.");

            var buffer = new byte[64 * 1024];
            EndPoint any = new IPEndPoint(IPAddress.Any, 0);

            while (true)
            {
                if (TryDequeueFrame(out var queued))
                    return queued;

#if NET8_0_OR_GREATER
                var received = await _socket.ReceiveFromAsync(buffer, SocketFlags.None, any, ct).ConfigureAwait(false);
#else
                var received = await _socket.ReceiveFromAsync(new ArraySegment<byte>(buffer), SocketFlags.None, any).ConfigureAwait(false);
#endif
                if (!EndPointEquals(received.RemoteEndPoint, _remote))
                    continue;

                ProcessInput(buffer.AsSpan(0, received.ReceivedBytes));

                if (TryDequeueFrame(out var frame))
                    return frame;
            }
        }

        public async ValueTask DisposeAsync()
        {
            IsConnected = false;
            _cts.Cancel();

            if (_updateLoop is not null)
            {
                try
                {
                    await _updateLoop.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }

            _cts.Dispose();
            _kcp?.Dispose();

            try
            {
                _socket?.Dispose();
            }
            catch
            {
            }
        }

        void IKcpCallback.Output(IMemoryOwner<byte> buffer, int avalidLength)
        {
            try
            {
                if (_socket is null || _remote is null)
                    return;

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

        private async Task<int> ReceiveHandshakeAckAsync(Socket socket, EndPoint bootstrapEndPoint, uint conv)
        {
            var buffer = new byte[32];
            EndPoint any = new IPEndPoint(IPAddress.Any, 0);

            while (true)
            {
#if NET8_0_OR_GREATER
                var received = await socket.ReceiveFromAsync(buffer, SocketFlags.None, any).ConfigureAwait(false);
#else
                var received = await socket.ReceiveFromAsync(new ArraySegment<byte>(buffer), SocketFlags.None, any).ConfigureAwait(false);
#endif
                if (!EndPointEquals(received.RemoteEndPoint, bootstrapEndPoint))
                    continue;

                var packet = buffer.AsSpan(0, received.ReceivedBytes);
                if (packet.Length != 12)
                    continue;
                if (!packet.Slice(0, 4).SequenceEqual("UACK"u8))
                    continue;
                if (BinaryPrimitives.ReadUInt32LittleEndian(packet.Slice(4, 4)) != conv)
                    continue;

                return BinaryPrimitives.ReadInt32LittleEndian(packet.Slice(8, 4));
            }
        }

        private void ProcessInput(ReadOnlySpan<byte> data)
        {
            lock (_kcpGate)
            {
                _kcp!.Input(data);
                DrainKcp();
            }
        }

        private void DrainKcp()
        {
            while (true)
            {
                var size = _kcp!.PeekSize();
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
            var oldLength = _accum.Length;
            Array.Resize(ref _accum, oldLength + payload.Length);
            payload.CopyTo(_accum.AsSpan(oldLength));

            while (true)
            {
                var sequence = new ReadOnlySequence<byte>(_accum);
                if (!LengthPrefix.TryUnpack(ref sequence, out var payloadSequence))
                    break;

                var frame = payloadSequence.ToArray();
                if (frame.Length > 0)
                    _frames.Enqueue(frame);

                _accum = sequence.ToArray();
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
                        if (_kcp is not null)
                        {
                            var now = DateTimeOffset.UtcNow;
                            _kcp.Update(in now);
                        }
                    }

                    await Task.Delay(UpdateIntervalMs, _cts.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private static byte[] CreateHandshakeRequest(uint conv)
        {
            var buffer = new byte[8];
            "UKCP"u8.CopyTo(buffer);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(4, 4), conv);
            return buffer;
        }

        private static uint CreateConversationId()
        {
            var guid = Guid.NewGuid().ToByteArray();
            var conv = BinaryPrimitives.ReadUInt32LittleEndian(guid);
            return conv == 0 ? 1u : conv;
        }

        private static async Task<IPAddress> ResolveHostAsync(string host)
        {
            if (IPAddress.TryParse(host, out var address))
                return address;

            var addresses = await Dns.GetHostAddressesAsync(host).ConfigureAwait(false);
            foreach (var candidate in addresses)
            {
                if (candidate.AddressFamily == AddressFamily.InterNetwork)
                    return candidate;
            }

            throw new InvalidOperationException($"Unable to resolve IPv4 endpoint for '{host}'.");
        }

        private static bool EndPointEquals(EndPoint? left, EndPoint? right)
        {
            return left is not null && right is not null && left.Equals(right);
        }
    }
}

using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.Sockets.Kcp;
using ULinkRPC.Core;

namespace ULinkRPC.Transport.Kcp
{
    /// <summary>
    ///     ITransport implementation over KCP (UDP).
    ///     Uses the same length-prefix framing (4-byte big-endian + payload) as other transports.
    /// </summary>
    public sealed class KcpServerTransport : ITransport, IKcpCallback, IRentable, IRemoteEndPointProvider
    {
        private const int MaxFrameSize = 64 * 1024 * 1024;
        private const int UpdateIntervalMs = 10;
        private readonly CancellationTokenSource _cts = new();
        private readonly ConcurrentQueue<ReadOnlyMemory<byte>> _frames = new();
        private readonly ReadOnlyMemory<byte> _initialPacket;
        private readonly SimpleSegManager.Kcp _kcp;
        private readonly object _kcpGate = new();
        private readonly bool _ownsSocket;
        private readonly EndPoint _remote;

        private readonly Socket _socket;
        private byte[] _accum = Array.Empty<byte>();

        private Task? _updateLoop;

        public KcpServerTransport(Socket socket, EndPoint remote, uint conv, ReadOnlyMemory<byte> initialPacket,
            bool ownsSocket = false)
        {
            _socket = socket ?? throw new ArgumentNullException(nameof(socket));
            _remote = remote ?? throw new ArgumentNullException(nameof(remote));
            _initialPacket = initialPacket;
            _ownsSocket = ownsSocket;

            _kcp = new SimpleSegManager.Kcp(conv, this, this);
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

        public EndPoint? RemoteEndPoint => _remote;

        public bool IsConnected { get; private set; }

        public ValueTask ConnectAsync(CancellationToken ct = default)
        {
            if (IsConnected)
                return default;

            IsConnected = true;
            _updateLoop = Task.Run(UpdateLoopAsync);

            if (!_initialPacket.IsEmpty)
                ProcessInput(_initialPacket.Span);

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
                res = await _socket.ReceiveFromAsync(buffer, SocketFlags.None, any, ct).ConfigureAwait(false);
#else
                res = await _socket.ReceiveFromAsync(new ArraySegment<byte>(buffer), SocketFlags.None, any)
                    .ConfigureAwait(false);
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
                    await _updateLoop.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }

            _cts.Dispose();
            _kcp.Dispose();

            if (_ownsSocket)
                try
                {
                    _socket.Dispose();
                }
                catch
                {
                }
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

                    await Task.Delay(UpdateIntervalMs, _cts.Token).ConfigureAwait(false);
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

using System.Net;
using System.Net.Sockets;
using ULinkRPC.Core;

namespace ULinkRPC.Transport.Tcp
{
    /// <summary>
    ///     ITransport implementation that wraps an accepted TcpClient (server side).
    ///     Uses the same length-prefix framing (4-byte big-endian + payload) as the Unity TcpTransport.
    /// </summary>
    public sealed class TcpServerTransport : ITransport, IRemoteEndPointProvider
    {
        private const int MaxFrameSize = 64 * 1024 * 1024;

        private readonly TcpClient _client;
        private readonly SemaphoreSlim _sendGate = new(1, 1);
        private bool _connected;
        private NetworkStream? _stream;

        public TcpServerTransport(TcpClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public EndPoint? RemoteEndPoint => _client.Client.RemoteEndPoint;

        public bool IsConnected => _connected && _client.Connected;

        public ValueTask ConnectAsync(CancellationToken ct = default)
        {
            if (_stream is not null)
                return default;

            _stream = _client.GetStream();
            _connected = true;
            return default;
        }

        public async ValueTask SendFrameAsync(ReadOnlyMemory<byte> frame, CancellationToken ct = default)
        {
            await _sendGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (_stream is null)
                    throw new InvalidOperationException("Not connected.");

                var packed = LengthPrefix.Pack(frame.Span);
                await _stream.WriteAsync(packed, ct).ConfigureAwait(false);
            }
            finally
            {
                _sendGate.Release();
            }
        }

        public async ValueTask<ReadOnlyMemory<byte>> ReceiveFrameAsync(CancellationToken ct = default)
        {
            if (_stream is null)
                throw new InvalidOperationException("Not connected.");

            var lenBuf = new byte[4];
            await ReadExactlyAsync(lenBuf, ct).ConfigureAwait(false);

            var len = ((uint)lenBuf[0] << 24) | ((uint)lenBuf[1] << 16) | ((uint)lenBuf[2] << 8) | lenBuf[3];

            if (len > MaxFrameSize)
                throw new InvalidOperationException($"Frame too large: {len} bytes");

            if (len == 0)
                return Array.Empty<byte>();

            var payload = new byte[len];
            await ReadExactlyAsync(payload, ct).ConfigureAwait(false);
            return payload;
        }

        public async ValueTask DisposeAsync()
        {
            _connected = false;
            try
            {
                _stream?.Dispose();
            }
            catch
            {
            }

            try
            {
                _client.Dispose();
            }
            catch
            {
            }

            _stream = null;
            await Task.CompletedTask.ConfigureAwait(false);
        }

        private async ValueTask ReadExactlyAsync(byte[] buffer, CancellationToken ct)
        {
            if (_stream is null)
                throw new InvalidOperationException("Not connected.");

            var offset = 0;
            var count = buffer.Length;

            while (count > 0)
            {
                var n = await _stream.ReadAsync(buffer.AsMemory(offset, count), ct).ConfigureAwait(false);
                if (n == 0)
                    throw new IOException("Remote closed the connection.");

                offset += n;
                count -= n;
            }
        }
    }
}

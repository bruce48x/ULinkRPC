using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using ULinkRPC.Core;

namespace ULinkRPC.Transport.Tcp
{
    public sealed class TcpTransport : ITransport
    {
        private const int MaxFrameSize = 64 * 1024 * 1024;

        private readonly string _host;
        private readonly int _port;
        private readonly SemaphoreSlim _sendGate = new(1, 1);
        private TcpClient? _client;
        private NetworkStream? _stream;
        private bool _connected;

        public TcpTransport(string host, int port)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _port = port;
        }

        public bool IsConnected => _connected && _client is not null && _client.Connected;

        public async ValueTask ConnectAsync(CancellationToken ct = default)
        {
            if (_stream is not null)
                return;

            _client = new TcpClient();
            await _client.ConnectAsync(_host, _port).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            _stream = _client.GetStream();
            _connected = true;
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

        public ValueTask DisposeAsync()
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
                _client?.Dispose();
            }
            catch
            {
            }

            _stream = null;
            _client = null;
            return default;
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

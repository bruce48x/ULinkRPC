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
        private TcpClient? _client;
        private TcpPipeFraming? _framing;
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
            _framing = new TcpPipeFraming(_stream, MaxFrameSize);
            _connected = true;
        }

        public async ValueTask SendFrameAsync(ReadOnlyMemory<byte> frame, CancellationToken ct = default)
        {
            if (_framing is null)
                throw new InvalidOperationException("Not connected.");

            await _framing.SendFrameAsync(frame, ct).ConfigureAwait(false);
        }

        public async ValueTask<ReadOnlyMemory<byte>> ReceiveFrameAsync(CancellationToken ct = default)
        {
            if (_framing is null)
                throw new InvalidOperationException("Not connected.");

            return await _framing.ReceiveFrameAsync(ct).ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            _connected = false;
            try
            {
                if (_framing is not null)
                    await _framing.DisposeAsync().ConfigureAwait(false);
            }
            catch
            {
            }

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

            _framing = null;
            _stream = null;
            _client = null;
        }
    }
}

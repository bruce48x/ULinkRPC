using System.Buffers;
using System.Net;
using System.Net.WebSockets;
using ULinkRPC.Core;
using NetWebSocket = System.Net.WebSockets.WebSocket;

namespace ULinkRPC.Transport.WebSocket
{
    /// <summary>
    ///     ITransport implementation that wraps an accepted WebSocket (server side).
    ///     Uses the same length-prefix framing (4-byte big-endian + payload) as the Unity WebSocketTransport.
    /// </summary>
    public sealed class WebSocketServerTransport : ITransport, IRemoteEndPointProvider
    {
        private const int MaxBufferedBytes = 64 * 1024 * 1024;
        private readonly NetWebSocket _ws;
        private byte[] _accum = Array.Empty<byte>();

        public WebSocketServerTransport(NetWebSocket ws, EndPoint? remoteEndPoint = null)
        {
            _ws = ws ?? throw new ArgumentNullException(nameof(ws));
            RemoteEndPoint = remoteEndPoint;
        }

        public EndPoint? RemoteEndPoint { get; }

        public bool IsConnected => _ws.State == WebSocketState.Open;

        public ValueTask ConnectAsync(CancellationToken ct = default)
        {
            return default;
        }

        public async ValueTask SendFrameAsync(ReadOnlyMemory<byte> frame, CancellationToken ct = default)
        {
            if (_ws.State != WebSocketState.Open)
                throw new InvalidOperationException("Not connected.");

            var packed = LengthPrefix.Pack(frame.Span);
            await _ws.SendAsync(packed, WebSocketMessageType.Binary, true, ct).ConfigureAwait(false);
        }

        public async ValueTask<ReadOnlyMemory<byte>> ReceiveFrameAsync(CancellationToken ct = default)
        {
            if (_ws.State != WebSocketState.Open)
                throw new InvalidOperationException("Not connected.");

            while (true)
            {
                var tmp = ArrayPool<byte>.Shared.Rent(8 * 1024);
                try
                {
                    var res = await _ws.ReceiveAsync(tmp, ct).ConfigureAwait(false);
                    if (res.MessageType == WebSocketMessageType.Close)
                        throw new IOException("WebSocket closed.");

                    var oldLen = _accum.Length;
                    if (oldLen + res.Count > MaxBufferedBytes)
                        throw new InvalidOperationException("WebSocket frame buffer exceeded maximum size.");

                    Array.Resize(ref _accum, oldLen + res.Count);
                    Array.Copy(tmp, 0, _accum, oldLen, res.Count);

                    var seq = new ReadOnlySequence<byte>(_accum);
                    if (LengthPrefix.TryUnpack(ref seq, out var payloadSeq))
                    {
                        var payload = payloadSeq.ToArray();
                        var remaining = seq.ToArray();
                        _accum = remaining;
                        return payload;
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(tmp);
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (_ws.State == WebSocketState.Open || _ws.State == WebSocketState.CloseReceived)
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None)
                        .ConfigureAwait(false);
            }
            catch
            {
            }

            _ws.Dispose();
            await Task.CompletedTask.ConfigureAwait(false);
        }
    }
}

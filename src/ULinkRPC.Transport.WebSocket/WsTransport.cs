using System.Net.WebSockets;
using ULinkRPC.Core;

namespace ULinkRPC.Transport.WebSocket;

public sealed class WsTransport : ITransport
{
    private readonly Uri _uri;
    private readonly ClientWebSocket _webSocket = new();
    private byte[] _accum = Array.Empty<byte>();

    public WsTransport(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("WebSocket URL is required.", nameof(url));

        _uri = new Uri(url, UriKind.Absolute);
    }

    public WsTransport(Uri uri)
    {
        _uri = uri ?? throw new ArgumentNullException(nameof(uri));
    }

    public bool IsConnected => _webSocket.State == WebSocketState.Open;

    public async ValueTask ConnectAsync(CancellationToken ct = default)
    {
        if (_webSocket.State == WebSocketState.Open)
            return;

        await _webSocket.ConnectAsync(_uri, ct).ConfigureAwait(false);
    }

    public ValueTask SendFrameAsync(ReadOnlyMemory<byte> frame, CancellationToken ct = default)
    {
        return WsTransportFraming.SendFrameAsync(_webSocket, frame, ct);
    }

    public ValueTask<ReadOnlyMemory<byte>> ReceiveFrameAsync(CancellationToken ct = default)
    {
        return WsTransportFraming.ReceiveFrameAsync(_webSocket, _accum, value => _accum = value, ct);
    }

    public ValueTask DisposeAsync()
    {
        return WsTransportFraming.DisposeAsync(_webSocket);
    }
}

using System.Net;
using ULinkRPC.Core;
using NetWebSocket = System.Net.WebSockets.WebSocket;

namespace ULinkRPC.Transport.WebSocket;

public sealed class WsServerTransport : ITransport, IRemoteEndPointProvider
{
    private readonly NetWebSocket _webSocket;
    private byte[] _accum = Array.Empty<byte>();

    public WsServerTransport(NetWebSocket webSocket, EndPoint? remoteEndPoint = null)
    {
        _webSocket = webSocket ?? throw new ArgumentNullException(nameof(webSocket));
        RemoteEndPoint = remoteEndPoint;
    }

    public EndPoint? RemoteEndPoint { get; }

    public bool IsConnected => _webSocket.State == System.Net.WebSockets.WebSocketState.Open;

    public ValueTask ConnectAsync(CancellationToken ct = default)
    {
        return default;
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

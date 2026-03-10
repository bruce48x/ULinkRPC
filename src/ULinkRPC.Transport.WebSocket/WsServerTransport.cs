using System.Net;
using ULinkRPC.Core;
using NetWebSocket = System.Net.WebSockets.WebSocket;

namespace ULinkRPC.Transport.WebSocket;

public sealed class WsServerTransport : ITransport, IRemoteEndPointProvider
{
    private readonly Action? _onDispose;
    private readonly NetWebSocket _webSocket;
    private byte[] _accum = Array.Empty<byte>();

    public WsServerTransport(NetWebSocket webSocket, EndPoint? remoteEndPoint = null, Action? onDispose = null)
    {
        _webSocket = webSocket ?? throw new ArgumentNullException(nameof(webSocket));
        RemoteEndPoint = remoteEndPoint;
        _onDispose = onDispose;
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
        return DisposeCoreAsync();
    }

    private async ValueTask DisposeCoreAsync()
    {
        try
        {
            await WsTransportFraming.DisposeAsync(_webSocket).ConfigureAwait(false);
        }
        finally
        {
            _onDispose?.Invoke();
        }
    }
}

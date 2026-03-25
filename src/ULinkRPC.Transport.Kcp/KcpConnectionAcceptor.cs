#if NET10_0_OR_GREATER
using System.Net;
using ULinkRPC.Core;

namespace ULinkRPC.Transport.Kcp;

public sealed class KcpConnectionAcceptor : IRpcConnectionAcceptor
{
    private readonly KcpListener _listener;

    public KcpConnectionAcceptor(int port)
    {
        _listener = new KcpListener(port);
    }

    public string ListenAddress
    {
        get
        {
            var endPoint = (IPEndPoint?)_listener.LocalEndPoint;
            return $"udp://0.0.0.0:{endPoint?.Port ?? 0}";
        }
    }

    public async ValueTask<RpcAcceptedConnection> AcceptAsync(CancellationToken ct = default)
    {
        var accepted = await _listener.AcceptAsync(ct).ConfigureAwait(false);
        return new RpcAcceptedConnection(
            accepted.Transport,
            $"{accepted.RemoteEndPoint} conv={accepted.ConversationId} localPort={accepted.LocalPort}",
            accepted.RemoteEndPoint);
    }

    public ValueTask DisposeAsync()
    {
        return _listener.DisposeAsync();
    }
}
#endif

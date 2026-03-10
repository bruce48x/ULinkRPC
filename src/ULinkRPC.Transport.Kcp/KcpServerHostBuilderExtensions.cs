#if NET10_0_OR_GREATER
using System.Net;
using ULinkRPC.Server;

namespace ULinkRPC.Transport.Kcp;

public static class KcpServerHostBuilderExtensions
{
    public static RpcServerHostBuilder UseKcp(this RpcServerHostBuilder builder, int defaultPort = 20000)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.UseAcceptor(_ =>
        {
            IRpcConnectionAcceptor acceptor = new KcpConnectionAcceptor(builder.ResolvePort(defaultPort));
            return ValueTask.FromResult(acceptor);
        });
    }

    private sealed class KcpConnectionAcceptor : IRpcConnectionAcceptor
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
}
#endif

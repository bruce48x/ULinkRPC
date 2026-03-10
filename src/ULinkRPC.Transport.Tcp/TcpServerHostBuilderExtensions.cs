#if NET10_0_OR_GREATER
using System.Net;
using System.Net.Sockets;
using ULinkRPC.Server;

namespace ULinkRPC.Transport.Tcp;

public static class TcpServerHostBuilderExtensions
{
    public static RpcServerHostBuilder UseTcp(this RpcServerHostBuilder builder, int defaultPort = 20000)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.UseAcceptor(_ =>
        {
            IRpcConnectionAcceptor acceptor = new TcpConnectionAcceptor(builder.ResolvePort(defaultPort));
            return ValueTask.FromResult(acceptor);
        });
    }

    private sealed class TcpConnectionAcceptor : IRpcConnectionAcceptor
    {
        private readonly TcpListener _listener;

        public TcpConnectionAcceptor(int port)
        {
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
        }

        public string ListenAddress
        {
            get
            {
                var endPoint = (IPEndPoint)_listener.LocalEndpoint;
                return $"tcp://0.0.0.0:{endPoint.Port}";
            }
        }

        public async ValueTask<RpcAcceptedConnection> AcceptAsync(CancellationToken ct = default)
        {
            var client = await _listener.AcceptTcpClientAsync(ct).ConfigureAwait(false);
            var remoteEndPoint = client.Client.RemoteEndPoint;
            return new RpcAcceptedConnection(
                new TcpServerTransport(client),
                remoteEndPoint?.ToString() ?? "?",
                remoteEndPoint);
        }

        public ValueTask DisposeAsync()
        {
            _listener.Stop();
            return default;
        }
    }
}
#endif

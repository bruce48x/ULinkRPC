using ULinkRPC.Client;

namespace ULinkRPC.Client;

public static class TcpClientBuilderExtensions
{
    public static RpcClientBuilder UseTcp(this RpcClientBuilder builder, string host, int port)
    {
        if (builder is null)
            throw new ArgumentNullException(nameof(builder));

        return builder.UseTransport(() => new global::ULinkRPC.Transport.Tcp.TcpTransport(host, port));
    }
}

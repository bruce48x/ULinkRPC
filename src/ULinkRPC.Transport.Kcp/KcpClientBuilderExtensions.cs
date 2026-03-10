using ULinkRPC.Client;

namespace ULinkRPC.Client;

public static class KcpClientBuilderExtensions
{
    public static RpcClientBuilder UseKcp(this RpcClientBuilder builder, string host, int port)
    {
        if (builder is null)
            throw new ArgumentNullException(nameof(builder));

        return builder.UseTransport(() => new global::ULinkRPC.Transport.Kcp.KcpTransport(host, port));
    }
}

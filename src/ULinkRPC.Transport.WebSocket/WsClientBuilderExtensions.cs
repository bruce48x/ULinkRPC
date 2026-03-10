using ULinkRPC.Client;

namespace ULinkRPC.Client;

public static class WsClientBuilderExtensions
{
    public static RpcClientBuilder UseWebSocket(this RpcClientBuilder builder, string url)
    {
        if (builder is null)
            throw new ArgumentNullException(nameof(builder));

        return builder.UseTransport(() => new global::ULinkRPC.Transport.WebSocket.WsTransport(url));
    }
}

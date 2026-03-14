using ULinkRPC.Core;

namespace ULinkRPC.Client;

public sealed class RpcClientOptions
{
    public RpcClientOptions(ITransport transport, IRpcSerializer serializer)
    {
        Transport = transport ?? throw new ArgumentNullException(nameof(transport));
        Serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    }

    public IRpcSerializer Serializer { get; }

    public ITransport Transport { get; }
}

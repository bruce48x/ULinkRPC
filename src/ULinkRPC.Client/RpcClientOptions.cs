using ULinkRPC.Core;

namespace ULinkRPC.Client;

public sealed class RpcClientOptions
{
    private TransportSecurityConfig? _security;

    public RpcClientOptions(ITransport transport, IRpcSerializer serializer)
    {
        Transport = transport ?? throw new ArgumentNullException(nameof(transport));
        Serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    }

    public RpcKeepAliveOptions KeepAlive { get; set; } = RpcKeepAliveOptions.Disabled;

    public IRpcSerializer Serializer { get; }

    public TransportSecurityConfig Security => _security ??= new TransportSecurityConfig();

    public ITransport Transport { get; }

    public RpcClientOptions UseSecurity(Action<TransportSecurityConfig> configure)
    {
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));

        configure(Security);
        return this;
    }

    public ITransport CreateConfiguredTransport()
    {
        return _security is { IsEnabled: true }
            ? new TransformingTransport(Transport, _security)
            : Transport;
    }
}

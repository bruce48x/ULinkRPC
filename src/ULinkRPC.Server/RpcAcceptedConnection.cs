using System.Net;
using ULinkRPC.Core;

namespace ULinkRPC.Server;

public sealed class RpcAcceptedConnection
{
    public RpcAcceptedConnection(ITransport transport, string displayName, EndPoint? remoteEndPoint = null)
    {
        Transport = transport ?? throw new ArgumentNullException(nameof(transport));
        DisplayName = string.IsNullOrWhiteSpace(displayName)
            ? throw new ArgumentException("Connection display name is required.", nameof(displayName))
            : displayName;
        RemoteEndPoint = remoteEndPoint;
    }

    public string DisplayName { get; }

    public EndPoint? RemoteEndPoint { get; }

    public ITransport Transport { get; }
}

using System.Collections.Concurrent;
using ULinkRPC.Core;

namespace ULinkRPC.Server;

public delegate ValueTask<RpcResponseEnvelope> RpcSessionHandler(RpcSession session, RpcRequestEnvelope req, CancellationToken ct);

public sealed class RpcServiceRegistry
{
    private readonly ConcurrentDictionary<(int serviceId, int methodId), RpcSessionHandler> _handlers = new();

    public void Register(int serviceId, int methodId, RpcSessionHandler handler)
    {
        if (handler is null) throw new ArgumentNullException(nameof(handler));
        _handlers[(serviceId, methodId)] = handler;
    }

    public bool TryGetHandler(int serviceId, int methodId, out RpcSessionHandler handler)
    {
        return _handlers.TryGetValue((serviceId, methodId), out handler!);
    }
}

namespace ULinkRPC.Core;

public interface IRpcConnectionAcceptor : IAsyncDisposable
{
    string ListenAddress { get; }

    ValueTask<RpcAcceptedConnection> AcceptAsync(CancellationToken ct = default);
}

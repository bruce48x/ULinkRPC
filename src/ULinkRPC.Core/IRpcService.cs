namespace ULinkRPC.Core
{
    /// <summary>
    ///     Marker interface for bidirectional RPC services.
    ///     TSelf is the service (client-to-server) interface.
    ///     TCallback is the callback (server-to-client push) interface.
    /// </summary>
    public interface IRpcService<TSelf, TCallback>
        where TSelf : IRpcService<TSelf, TCallback>
    {
    }
}

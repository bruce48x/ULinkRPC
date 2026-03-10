namespace ULinkRPC.Client;

public sealed class RpcClientConnection<TApi> : IAsyncDisposable
{
    public RpcClientConnection(RpcClient client, TApi api)
    {
        if (client is null)
            throw new ArgumentNullException(nameof(client));

        if (api is null)
            throw new ArgumentNullException(nameof(api));

        Client = client;
        Api = api;
    }

    public TApi Api { get; }

    public RpcClient Client { get; }

    public ValueTask DisposeAsync()
    {
        return Client.DisposeAsync();
    }
}

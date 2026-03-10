using ULinkRPC.Core;

namespace ULinkRPC.Client;

public sealed class RpcClientBuilder
{
    private readonly List<Action<IRpcClient>> _clientConfigurators = new();
    private IRpcSerializer? _serializer;
    private Func<ITransport>? _transportFactory;

    public static RpcClientBuilder Create()
    {
        return new RpcClientBuilder();
    }

    public RpcClientBuilder UseTransport(ITransport transport)
    {
        if (transport is null)
            throw new ArgumentNullException(nameof(transport));

        return UseTransport(() => transport);
    }

    public RpcClientBuilder UseTransport(Func<ITransport> transportFactory)
    {
        _transportFactory = transportFactory ?? throw new ArgumentNullException(nameof(transportFactory));
        return this;
    }

    public RpcClientBuilder UseSerializer(IRpcSerializer serializer)
    {
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        return this;
    }

    public RpcClientBuilder ConfigureClient(Action<IRpcClient> configure)
    {
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));

        _clientConfigurators.Add(configure);
        return this;
    }

    public RpcClient Build()
    {
        if (_transportFactory is null)
            throw new InvalidOperationException("RPC transport is not configured.");

        if (_serializer is null)
            throw new InvalidOperationException("RPC serializer is not configured.");

        return new RpcClient(_transportFactory(), _serializer);
    }

    public ValueTask<RpcClient> ConnectAsync(CancellationToken ct = default)
    {
        return ConnectTypedAsync(static client => client, configureClient: null, ct);
    }

    public async ValueTask<TConnection> ConnectTypedAsync<TConnection>(
        Func<RpcClient, TConnection> connectionFactory,
        Action<IRpcClient>? configureClient = null,
        CancellationToken ct = default)
    {
        if (connectionFactory is null)
            throw new ArgumentNullException(nameof(connectionFactory));

        var client = Build();
        try
        {
            ApplyClientConfiguration(client);
            configureClient?.Invoke(client);
            await client.StartAsync(ct).ConfigureAwait(false);
            return connectionFactory(client);
        }
        catch
        {
            await client.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async ValueTask<RpcClientConnection<TApi>> ConnectApiAsync<TApi>(
        Func<RpcClient, TApi> apiFactory,
        CancellationToken ct = default)
    {
        if (apiFactory is null)
            throw new ArgumentNullException(nameof(apiFactory));

        return await ConnectTypedAsync(
            client => new RpcClientConnection<TApi>(client, apiFactory(client)),
            configureClient: null,
            ct).ConfigureAwait(false);
    }

    private void ApplyClientConfiguration(IRpcClient client)
    {
        foreach (var configure in _clientConfigurators)
            configure(client);
    }
}

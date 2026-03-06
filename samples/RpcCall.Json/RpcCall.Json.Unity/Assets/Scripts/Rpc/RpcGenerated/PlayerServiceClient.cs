using System;
using System.Threading;
using System.Threading.Tasks;
using Game.Rpc.Contracts;
using ULinkRPC.Core;

namespace Rpc.Generated
{
    public sealed class PlayerServiceClient : IPlayerService
    {
        private const int ServiceId = 1;
        private static readonly RpcMethod<LoginRequest, LoginReply> loginAsyncRpcMethod = new(ServiceId, 1);
        private static readonly RpcMethod<RpcVoid, RpcVoid> pingAsyncRpcMethod = new(ServiceId, 2);

        private readonly IRpcClient _client;

        public PlayerServiceClient(IRpcClient client) { _client = client; }

        public ValueTask<LoginReply> LoginAsync(LoginRequest req)
        {
            return LoginAsync(req, CancellationToken.None);
        }

        public ValueTask<LoginReply> LoginAsync(LoginRequest req, CancellationToken ct)
        {
            return _client.CallAsync(loginAsyncRpcMethod, req, ct);
        }

        public async ValueTask PingAsync()
        {
            await PingAsync(CancellationToken.None);
        }

        public async ValueTask PingAsync(CancellationToken ct)
        {
            await _client.CallAsync(pingAsyncRpcMethod, default, ct);
        }

    }

    public static class PlayerServiceClientExtensions
    {
        public static IPlayerService CreatePlayerService(this IRpcClient client)
        {
            if (client is null) throw new ArgumentNullException(nameof(client));
            return new PlayerServiceClient(client);
        }
    }
}

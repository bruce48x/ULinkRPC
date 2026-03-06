using System.Threading.Tasks;
using ULinkRPC.Core;

namespace Game.Rpc.Contracts
{
    [RpcService(1)]
    public interface IPlayerService : IRpcService<IPlayerService, IPlayerCallback>
    {
        [RpcMethod(1)]
        ValueTask<LoginReply> LoginAsync(LoginRequest req);

        [RpcMethod(2)]
        ValueTask PingAsync();
    }

    public interface IPlayerCallback
    {
        [RpcMethod(1)]
        void OnNotify(string message);
    }
}

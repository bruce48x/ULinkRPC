using System.Threading.Tasks;
using ULinkRPC.Core;

namespace Game.Rpc.Contracts
{
    [RpcService(2)]
    public interface IInventoryService : IRpcService<IInventoryService, IInventoryCallback>
    {
        [RpcMethod(1)]
        ValueTask<LoginReply> LoginAsync(LoginRequest req);

        [RpcMethod(2)]
        ValueTask<int> IncrRevision();
    }

    public interface IInventoryCallback
    {
        [RpcMethod(1)]
        void OnInventoryNotify(string message);
    }
}

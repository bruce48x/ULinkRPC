using System.Threading.Tasks;
using ULinkRPC.Core;

namespace Game.Rpc.Contracts
{
    [RpcService(2, Callback = typeof(IInventoryCallback))]
    public interface IInventoryService
    {
        [RpcMethod(1)]
        ValueTask<LoginReply> LoginAsync(LoginRequest req);

        [RpcMethod(2)]
        ValueTask<int> IncrRevision();
    }

    [RpcCallback(typeof(IInventoryService))]
    public interface IInventoryCallback
    {
        [RpcPush(1)]
        void OnInventoryNotify(string message);
    }
}

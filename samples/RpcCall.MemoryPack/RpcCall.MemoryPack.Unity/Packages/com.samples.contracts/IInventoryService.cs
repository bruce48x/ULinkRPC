using System.Threading.Tasks;
using ULinkRPC.Core;

namespace Game.Rpc.Contracts
{
    [RpcService(2, Callback = typeof(IInventoryCallback))]
    public interface IInventoryService
    {
        [RpcMethod(1)]
        ValueTask<RevisionReply> GetRevisionAsync(RevisionRequest req);

        [RpcMethod(2)]
        ValueTask<RevisionReply> IncrRevision(RevisionRequest req);
    }

    [RpcCallback(typeof(IInventoryService))]
    public interface IInventoryCallback
    {
        [RpcPush(1)]
        void OnInventoryNotify(InventoryNotify notify);
    }
}

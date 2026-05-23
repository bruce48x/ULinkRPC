using System.Threading.Tasks;
using ULinkRPC.Core;

namespace Game.Rpc.Contracts
{
    [RpcService(RpcContractIds.Services.Inventory, Callback = typeof(IInventoryCallback))]
    public interface IInventoryService
    {
        [RpcMethod(RpcContractIds.InventoryServiceMethods.GetRevisionAsync)]
        ValueTask<RevisionReply> GetRevisionAsync(RevisionRequest req);

        [RpcMethod(RpcContractIds.InventoryServiceMethods.IncrRevision)]
        ValueTask<RevisionReply> IncrRevision(RevisionRequest req);
    }

    [RpcCallback(typeof(IInventoryService))]
    public interface IInventoryCallback
    {
        [RpcPush(RpcContractIds.InventoryCallbackPushes.OnInventoryNotify)]
        void OnInventoryNotify(InventoryNotify notify);
    }
}

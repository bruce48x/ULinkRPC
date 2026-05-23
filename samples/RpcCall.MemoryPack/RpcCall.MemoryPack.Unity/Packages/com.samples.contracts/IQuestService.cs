using System.Threading.Tasks;
using ULinkRPC.Core;

namespace Game.Rpc.Contracts
{
    [RpcService(RpcContractIds.Services.Quest, Callback = typeof(IQuestCallback))]
    public interface IQuestService
    {
        [RpcMethod(RpcContractIds.QuestServiceMethods.GetProgressAsync)]
        ValueTask<ProgressReply> GetProgressAsync(ProgressRequest req);

        [RpcMethod(RpcContractIds.QuestServiceMethods.IncrProgress)]
        ValueTask<ProgressReply> IncrProgress(ProgressRequest req);
    }

    [RpcCallback(typeof(IQuestService))]
    public interface IQuestCallback
    {
        [RpcPush(RpcContractIds.QuestCallbackPushes.OnQuestNotify)]
        void OnQuestNotify(QuestNotify notify);
    }
}

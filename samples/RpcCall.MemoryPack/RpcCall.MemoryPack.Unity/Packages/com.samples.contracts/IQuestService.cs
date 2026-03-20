using System.Threading.Tasks;
using ULinkRPC.Core;

namespace Game.Rpc.Contracts
{
    [RpcService(3, Callback = typeof(IQuestCallback))]
    public interface IQuestService
    {
        [RpcMethod(1)]
        ValueTask<ProgressReply> GetProgressAsync(ProgressRequest req);

        [RpcMethod(2)]
        ValueTask<ProgressReply> IncrProgress(ProgressRequest req);
    }

    [RpcCallback(typeof(IQuestService))]
    public interface IQuestCallback
    {
        [RpcPush(1)]
        void OnQuestNotify(QuestNotify notify);
    }
}

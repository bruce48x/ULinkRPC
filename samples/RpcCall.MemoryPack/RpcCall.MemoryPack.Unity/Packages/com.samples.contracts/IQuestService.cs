using System.Threading.Tasks;
using ULinkRPC.Core;

namespace Game.Rpc.Contracts
{
    [RpcService(3, Callback = typeof(IQuestCallback))]
    public interface IQuestService
    {
        [RpcMethod(1)]
        ValueTask<LoginReply> LoginAsync(LoginRequest req);

        [RpcMethod(2)]
        ValueTask<int> IncrProgress();
    }

    [RpcCallback(typeof(IQuestService))]
    public interface IQuestCallback
    {
        [RpcPush(1)]
        void OnQuestNotify(string message);
    }
}

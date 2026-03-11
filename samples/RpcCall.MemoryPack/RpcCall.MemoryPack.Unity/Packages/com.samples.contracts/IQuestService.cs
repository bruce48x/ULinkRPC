using System.Threading.Tasks;
using ULinkRPC.Core;

namespace Game.Rpc.Contracts
{
    [RpcService(3)]
    public interface IQuestService : IRpcService<IQuestService, IQuestCallback>
    {
        [RpcMethod(1)]
        ValueTask<LoginReply> LoginAsync(LoginRequest req);

        [RpcMethod(2)]
        ValueTask<int> IncrProgress();
    }

    public interface IQuestCallback
    {
        [RpcMethod(1)]
        void OnQuestNotify(string message);
    }
}

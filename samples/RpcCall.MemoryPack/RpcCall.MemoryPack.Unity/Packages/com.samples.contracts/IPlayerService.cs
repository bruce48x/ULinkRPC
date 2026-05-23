using System.Threading.Tasks;
using ULinkRPC.Core;

namespace Game.Rpc.Contracts
{
    [RpcService(RpcContractIds.Services.Player, Callback = typeof(IPlayerCallback))]
    public interface IPlayerService
    {
        [RpcMethod(RpcContractIds.PlayerServiceMethods.LoginAsync)]
        ValueTask<LoginReply> LoginAsync(LoginRequest req);

        [RpcMethod(RpcContractIds.PlayerServiceMethods.IncrStep)]
        ValueTask<StepReply> IncrStep(StepRequest req);
    }

    [RpcCallback(typeof(IPlayerService))]
    public interface IPlayerCallback
    {
        [RpcPush(RpcContractIds.PlayerCallbackPushes.OnPlayerNotify)]
        void OnPlayerNotify(PlayerNotify notify);
    }
}

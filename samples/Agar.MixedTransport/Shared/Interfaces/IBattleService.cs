using System.Threading.Tasks;
using ULinkRPC.Core;

namespace Shared.Interfaces;

[RpcService(RpcContractIds.Services.Battle, Callback = typeof(IBattleCallback))]
public interface IBattleService
{
    [RpcMethod(RpcContractIds.BattleServiceMethods.JoinAsync)]
    ValueTask<BattleJoinReply> JoinAsync(BattleJoinRequest request);

    [RpcMethod(RpcContractIds.BattleServiceMethods.UpdateInputAsync)]
    ValueTask<CommandReply> UpdateInputAsync(PlayerInputRequest request);
}

[RpcCallback(typeof(IBattleService))]
public interface IBattleCallback
{
    [RpcPush(RpcContractIds.BattleCallbackPushes.OnSnapshot)]
    void OnSnapshot(WorldSnapshotReply snapshot);
}

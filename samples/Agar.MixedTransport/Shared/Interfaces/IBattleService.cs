using System.Threading.Tasks;
using ULinkRPC.Core;

namespace Shared.Interfaces;

[RpcService(2, Callback = typeof(IBattleCallback))]
public interface IBattleService
{
    [RpcMethod(1)]
    ValueTask<BattleJoinReply> JoinAsync(BattleJoinRequest request);

    [RpcMethod(2)]
    ValueTask<CommandReply> UpdateInputAsync(PlayerInputRequest request);
}

[RpcCallback(typeof(IBattleService))]
public interface IBattleCallback
{
    [RpcPush(1)]
    void OnSnapshot(WorldSnapshotReply snapshot);
}

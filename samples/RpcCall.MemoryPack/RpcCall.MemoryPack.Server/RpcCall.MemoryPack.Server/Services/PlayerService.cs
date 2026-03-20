using Game.Rpc.Contracts;

namespace RpcCall.MemoryPack.Server.Services;

public class PlayerService: IPlayerService
{
    private readonly IPlayerCallback _callback;
    private int _step;

    public PlayerService(IPlayerCallback callback)
    {
        _callback = callback;
    }

    public ValueTask<LoginReply> LoginAsync(LoginRequest req)
    {
        _callback.OnPlayerNotify(new PlayerNotify
        {
            Message = $"Welcome {req.Account}, player login accepted."
        });

        // Example: accept any account, return a dummy token.
        // Replace with your own auth logic.
        return new ValueTask<LoginReply>(new LoginReply
        {
            Code = 0,
            Token = $"token-{req.Account}-{Guid.NewGuid():N}"
        });
    }

    public ValueTask<StepReply> IncrStep(StepRequest req)
    {
        _step++;
        _callback.OnPlayerNotify(new PlayerNotify
        {
            Message = $"Player step => {_step}"
        });
        return new ValueTask<StepReply>(new StepReply
        {
            Step = _step
        });
    }
}

using Game.Rpc.Contracts;

namespace RpcCall.Json.Server.Services;

public class PlayerService: IPlayerService
{
    private readonly IPlayerCallback _callback;

    public PlayerService(IPlayerCallback callback)
    {
        _callback = callback;
    }

    public ValueTask<LoginReply> LoginAsync(LoginRequest req)
    {
        _callback.OnNotify($"Welcome {req.Account}, login request accepted.");

        // Example: accept any account, return a dummy token.
        // Replace with your own auth logic.
        return new ValueTask<LoginReply>(new LoginReply
        {
            Code = 0,
            Token = $"token-{req.Account}-{Guid.NewGuid():N}"
        });
    }

    public ValueTask PingAsync()
    {
        _callback.OnNotify("Ping received by server.");
        return default;
    }
}

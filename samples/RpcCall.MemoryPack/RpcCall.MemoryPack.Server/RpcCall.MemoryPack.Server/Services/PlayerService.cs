using Game.Rpc.Contracts;

namespace RpcCall.MemoryPack.Server.Services;

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

    public ValueTask<string> ComposeGreetingAsync(string name, int level, bool vip)
    {
        var tag = vip ? "VIP" : "NORMAL";
        var greeting = $"Hello {name}, Lv.{level} [{tag}]";
        _callback.OnNotify($"ComposeGreeting generated: {greeting}");
        return new ValueTask<string>(greeting);
    }
}

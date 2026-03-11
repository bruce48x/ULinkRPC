using Game.Rpc.Contracts;

namespace RpcCall.MemoryPack.Server.Services;

public class QuestService : IQuestService
{
    private readonly IQuestCallback _callback;
    private int _progress;

    public QuestService(IQuestCallback callback)
    {
        _callback = callback;
    }

    public ValueTask<LoginReply> LoginAsync(LoginRequest req)
    {
        _callback.OnQuestNotify($"Quest tracker ready for {req.Account}.");
        return new ValueTask<LoginReply>(new LoginReply
        {
            Code = 0,
            Token = $"quest-{req.Account}-{Guid.NewGuid():N}"
        });
    }

    public ValueTask<int> IncrProgress()
    {
        _progress++;
        _callback.OnQuestNotify($"Quest progress => {_progress}");
        return new ValueTask<int>(_progress);
    }
}

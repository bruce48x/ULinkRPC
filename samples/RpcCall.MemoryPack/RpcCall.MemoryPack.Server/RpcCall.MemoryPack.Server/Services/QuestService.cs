using Game.Rpc.Contracts;

namespace RpcCall.MemoryPack.Server.Services;

public class QuestService : IQuestService
{
    private readonly IQuestCallback _callback;
    private int _progress;
    private bool _announced;

    public QuestService(IQuestCallback callback)
    {
        _callback = callback;
    }

    public ValueTask<int> GetProgressAsync()
    {
        if (!_announced)
        {
            _announced = true;
            _callback.OnQuestNotify("Quest tracker ready.");
        }

        return new ValueTask<int>(_progress);
    }

    public ValueTask<int> IncrProgress()
    {
        _progress++;
        _callback.OnQuestNotify($"Quest progress => {_progress}");
        return new ValueTask<int>(_progress);
    }
}

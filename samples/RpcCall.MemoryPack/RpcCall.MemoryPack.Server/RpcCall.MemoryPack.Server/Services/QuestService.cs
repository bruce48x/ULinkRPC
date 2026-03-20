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

    public ValueTask<ProgressReply> GetProgressAsync(ProgressRequest req)
    {
        if (!_announced)
        {
            _announced = true;
            _callback.OnQuestNotify(new QuestNotify
            {
                Message = "Quest tracker ready."
            });
        }

        return new ValueTask<ProgressReply>(new ProgressReply
        {
            Progress = _progress
        });
    }

    public ValueTask<ProgressReply> IncrProgress(ProgressRequest req)
    {
        _progress++;
        _callback.OnQuestNotify(new QuestNotify
        {
            Message = $"Quest progress => {_progress}"
        });
        return new ValueTask<ProgressReply>(new ProgressReply
        {
            Progress = _progress
        });
    }
}

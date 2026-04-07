using System.Collections.Concurrent;

namespace ULinkRPC.Server;

internal sealed class TrackedTaskCollection
{
    private readonly ConcurrentDictionary<int, Task> _tasks = new();
    private int _nextId;

    public void Track(Task task)
    {
        ArgumentNullException.ThrowIfNull(task);

        var taskId = Interlocked.Increment(ref _nextId);
        _tasks[taskId] = task;

        _ = task.ContinueWith(
            _ =>
            {
                _tasks.TryRemove(taskId, out Task? _);
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    public async ValueTask WaitAsync()
    {
        while (true)
        {
            var tasks = _tasks.Values.ToArray();
            if (tasks.Length == 0)
                return;

            try
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            catch
            {
            }

            if (_tasks.IsEmpty)
                return;
        }
    }
}

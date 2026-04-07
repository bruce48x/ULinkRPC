using System.Collections.Concurrent;
using ULinkRPC.Core;

namespace ULinkRPC.Client;

internal sealed class RpcPendingRequestCollection
{
    private readonly ConcurrentDictionary<uint, TaskCompletionSource<RpcResponseEnvelope>> _pending = new();

    public TaskCompletionSource<RpcResponseEnvelope> Add(uint requestId)
    {
        var tcs = new TaskCompletionSource<RpcResponseEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(requestId, tcs))
            throw new InvalidOperationException($"RPC request id collision: {requestId}");

        return tcs;
    }

    public void Remove(uint requestId)
    {
        _pending.TryRemove(requestId, out _);
    }

    public bool TryCancel(uint requestId, CancellationToken ct)
    {
        if (_pending.TryRemove(requestId, out var pending))
        {
            pending.TrySetCanceled(ct);
            return true;
        }

        return false;
    }

    public bool TrySetResult(RpcResponseEnvelope response)
    {
        if (_pending.TryRemove(response.RequestId, out var pending))
        {
            pending.TrySetResult(response);
            return true;
        }

        return false;
    }

    public void FailAll(Exception ex)
    {
        foreach (var item in _pending)
        {
            if (_pending.TryRemove(item.Key, out var pending))
                pending.TrySetException(ex);
        }
    }
}

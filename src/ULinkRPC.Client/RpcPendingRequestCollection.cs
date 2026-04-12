using System.Collections.Concurrent;
using ULinkRPC.Core;

namespace ULinkRPC.Client;

internal sealed class RpcPendingRequestCollection
{
    private readonly ConcurrentDictionary<uint, TaskCompletionSource<RpcResponseEnvelope>> _pending = new();

    public (uint RequestId, TaskCompletionSource<RpcResponseEnvelope> CompletionSource) Reserve(ref int nextRequestId)
    {
        for (uint attempts = 0; attempts < uint.MaxValue; attempts++)
        {
            var requestId = unchecked((uint)Interlocked.Increment(ref nextRequestId));
            if (requestId == 0)
                continue;

            var tcs = new TaskCompletionSource<RpcResponseEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (_pending.TryAdd(requestId, tcs))
                return (requestId, tcs);
        }

        throw new InvalidOperationException("No RPC request id available; too many pending requests.");
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

using System;
using System.Threading;
using System.Threading.Tasks;

namespace ULinkRPC.Core
{
    public interface IRpcClient
    {
        ValueTask<TResult> CallAsync<TArg, TResult>(RpcMethod<TArg, TResult> method, TArg? arg,
            CancellationToken ct = default);

        void RegisterPushHandler<TArg>(RpcPushMethod<TArg> method, Action<TArg> handler);
    }
}
